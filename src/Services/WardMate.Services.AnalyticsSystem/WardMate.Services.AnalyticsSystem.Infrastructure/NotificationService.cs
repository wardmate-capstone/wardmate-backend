using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationService(NotificationDbContext db, IHubContext<NotificationHub, INotificationClient> hub,
    TimeProvider clock, ILogger<NotificationService> logger) : INotificationService
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
            RecipientUserId = request.RecipientUserId, ApplicationId = request.ApplicationId,
            Title = request.Title.Trim(), Content = request.Content, Type = request.Type,
            ActionUrl = request.ActionUrl, CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        db.Notifications.Add(entity);
        await db.SaveChangesAsync(ct);
        var dto = ToDto(entity);
        // Persistence succeeds independently of the recipient being connected or transport delivery failing.
        try
        {
            var client = hub.Clients.Group(NotificationHub.UserGroup(entity.RecipientUserId));
            await client.ReceiveNotification(dto);
            await client.UpdateUnreadCount(await UnreadCountAsync(entity.RecipientUserId, ct));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không gửi được realtime cho thông báo {NotificationId}; bản ghi đã được lưu.", entity.Id);
        }
        return dto;
    }

    public Task<int> UnreadCountAsync(Guid recipient, CancellationToken ct = default) =>
        db.Notifications.CountAsync(x => x.RecipientUserId == recipient && x.Channel == "IN_APP" && !x.IsRead, ct);

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid recipient, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || page > 1_000_000 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        return await db.Notifications.AsNoTracking().Where(x => x.RecipientUserId == recipient && x.Channel == "IN_APP")
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new NotificationDto(x.Id, x.RecipientUserId, x.ApplicationId, x.Title, x.Content,
                x.Type, x.Channel, x.IsRead, x.ReadAt, x.ActionUrl, x.CreatedAt)).ToListAsync(ct);
    }

    public async Task<bool> MarkReadAsync(Guid recipient, Guid notificationId, CancellationToken ct = default)
    {
        var owned = db.Notifications.Where(x => x.Id == notificationId && x.RecipientUserId == recipient && x.Channel == "IN_APP");
        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await owned.Where(x => !x.IsRead).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, now), ct);
        if (changed == 0 && !await owned.AnyAsync(ct)) return false;
        try { await hub.Clients.Group(NotificationHub.UserGroup(recipient)).UpdateUnreadCount(await UnreadCountAsync(recipient, ct)); }
        catch (Exception exception) { logger.LogWarning(exception, "Không cập nhật được bộ đếm realtime; trạng thái đã đọc đã được lưu."); }
        return true;
    }

    private static NotificationDto ToDto(Notification n) => new(n.Id, n.RecipientUserId, n.ApplicationId,
        n.Title, n.Content, n.Type, n.Channel, n.IsRead, n.ReadAt, n.ActionUrl, n.CreatedAt);
}
