using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationService(NotificationDbContext db, IHubContext<NotificationHub, INotificationClient> hub,
    TimeProvider clock, ILogger<NotificationService> logger, NotificationConnectionGuard guard) : INotificationService
{
    public async Task<NotificationDto> CreateAsync(CreateNotification request, CancellationToken ct = default)
    {
        if (request.RecipientUserId == Guid.Empty || request.ApplicationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 255 || string.IsNullOrWhiteSpace(request.Content) ||
            request.Type is not ("APPLICATION_STATUS_CHANGED" or "INLINE_COMMENT_ADDED" or "ASSIGNED_OFFICER" or "SYSTEM_ALERT") ||
            request.ActionUrl is { } url && (url.Length > 500 || !url.StartsWith('/') || url.StartsWith("//") ||
                url.Contains('\\') || url.Any(char.IsControl)))
            throw new ArgumentException("Thông báo không hợp lệ; đường dẫn thao tác phải là đường dẫn nội bộ bắt đầu bằng /.");

        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = request.RecipientUserId, ApplicationId = request.ApplicationId,
            Title = request.Title.Trim(), Content = request.Content, Type = request.Type,
            ActionUrl = request.ActionUrl, CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        db.Notifications.Add(entity);
        db.Deliveries.Add(new() { NotificationId = entity.Id });
        await db.SaveChangesAsync(ct);
        // A worker sends only after commit; a process crash cannot lose the pending delivery.
        return ToDto(entity);
    }

    public Task<int> UnreadCountAsync(Guid recipient, CancellationToken ct = default, string? wardCode = null) =>
        Unread(db.Notifications, recipient, wardCode).CountAsync(ct);

    /// <summary>Unread IN_APP notifications the caller may see: own rows, personal or of the caller's current ward.</summary>
    public static IQueryable<Notification> Unread(IQueryable<Notification> source, Guid recipient, string? wardCode, DateTime? before = null)
    {
        var query = source.Where(x => x.RecipientUserId == recipient && x.Channel == "IN_APP" && !x.IsRead && (x.RecipientWardCode == null || x.RecipientWardCode == wardCode));
        return before is { } cutoff ? query.Where(x => x.CreatedAt <= cutoff) : query;
    }

    /// <summary>SignalR may bind an ISO time without offset as Unspecified; Npgsql timestamptz needs UTC.</summary>
    public static DateTime? AsUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } other => DateTime.SpecifyKind(other, DateTimeKind.Utc)
    };

    public async Task<int> MarkAllReadAsync(Guid recipient, DateTime? before, CancellationToken ct = default, string? wardCode = null)
    {
        var scope = Unread(db.Notifications, recipient, wardCode, AsUtc(before));
        // One updated row (personal first) is enough to make every replica recount this user's open connections.
        var signal = await scope.OrderBy(x => x.RecipientWardCode != null).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (signal is null) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await scope.ExecuteUpdateAsync(set => set.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, now), ct);
        if (changed == 0) return 0;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_notify({NotificationRealtimeListener.Channel}, {signal.Value.ToString()})", ct);
            var connections = await guard.ValidatedConnections(recipient, wardCode, ct);
            await hub.Clients.Clients(connections).UpdateUnreadCount(await UnreadCountAsync(recipient, ct, wardCode));
        }
        catch (Exception exception) { logger.LogWarning(exception, "Không cập nhật được bộ đếm realtime; trạng thái đã đọc đã được lưu."); }
        return changed;
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid recipient, int page, int pageSize, CancellationToken ct = default, string? wardCode = null)
    {
        if (page < 1 || page > 1_000_000 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        return await db.Notifications.AsNoTracking().Where(x => x.RecipientUserId == recipient && x.Channel == "IN_APP" && (x.RecipientWardCode == null || x.RecipientWardCode == wardCode))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new NotificationDto(x.Id, x.RecipientUserId, x.ApplicationId, x.Title, x.Content,
                x.Type, x.Channel, x.IsRead, x.ReadAt, x.ActionUrl, x.CreatedAt)).ToListAsync(ct);
    }

    public async Task<bool> MarkReadAsync(Guid recipient, Guid notificationId, CancellationToken ct = default, string? wardCode = null)
    {
        var owned = db.Notifications.Where(x => x.Id == notificationId && x.RecipientUserId == recipient && x.Channel == "IN_APP" && (x.RecipientWardCode == null || x.RecipientWardCode == wardCode));
        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await owned.Where(x => !x.IsRead).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, now), ct);
        if (changed == 0 && !await owned.AnyAsync(ct)) return false;
        try
        {
            var signalId = notificationId.ToString();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_notify({NotificationRealtimeListener.Channel}, {signalId})", ct);
            var connections = await guard.ValidatedConnections(recipient, wardCode, ct);
            await hub.Clients.Clients(connections).UpdateUnreadCount(await UnreadCountAsync(recipient, ct, wardCode));
        }
        catch (Exception exception) { logger.LogWarning(exception, "Không cập nhật được bộ đếm realtime; trạng thái đã đọc đã được lưu."); }
        return true;
    }

    private static NotificationDto ToDto(Notification n) => new(n.Id, n.RecipientUserId, n.ApplicationId,
        n.Title, n.Content, n.Type, n.Channel, n.IsRead, n.ReadAt, n.ActionUrl, n.CreatedAt);
}
