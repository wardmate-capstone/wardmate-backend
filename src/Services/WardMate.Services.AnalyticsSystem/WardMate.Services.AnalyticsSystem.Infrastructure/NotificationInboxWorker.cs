using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationInboxWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NotificationInboxWorker> logger) : BackgroundService
{
    public static Notification Create(WorkflowEvent e, Guid recipient, bool officer) => new()
    {
        Id = Guid.NewGuid(), SourceEventId = e.EventId, RecipientUserId = recipient, ApplicationId = e.ApplicationId,
        RecipientWardCode = officer ? e.WardCode : null,
        Title = officer ? e.Status == "UNDER_REVIEW" ? "Hồ sơ được phân công" : "Có hồ sơ cần tiếp nhận" : e.Kind == "COMMENT" ? "Hồ sơ có nhận xét mới" : e.Status switch
        {
            "SUBMITTED" => "Hồ sơ đã được nộp", "UNDER_REVIEW" => "Hồ sơ đang được thẩm định", "NEED_REVISION" => "Hồ sơ yêu cầu bổ sung",
            "APPROVED" => "Hồ sơ đã được phê duyệt", "REJECTED" => "Hồ sơ đã bị từ chối", _ => "Trạng thái hồ sơ đã thay đổi"
        },
        Content = $"Hồ sơ {e.ApplicationCode} có cập nhật. Vui lòng mở hồ sơ để xem chi tiết.",
        Type = officer && e.Status == "UNDER_REVIEW" ? "ASSIGNED_OFFICER" : e.Kind == "COMMENT" ? "INLINE_COMMENT_ADDED" : "APPLICATION_STATUS_CHANGED",
        ActionUrl = officer ? $"/officer/applications/{e.ApplicationId}/review" : $"/applications/{e.ApplicationId}/edit",
        CreatedAt = e.OccurredAt
    };

    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Inbox.FromSqlRaw("SELECT * FROM notification_inbox WHERE \"State\" = 'Pending' AND \"NextAttemptAt\" <= now() ORDER BY \"CreatedAt\", \"Id\" LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if (row is null) return false;
        try
        {
            var e = JsonSerializer.Deserialize<WorkflowEvent>(row.Payload)!;
            if (!WorkflowNotificationConsumer.Valid(e)) throw new InvalidOperationException("Sự kiện không hợp lệ.");
            var contacts = await scope.ServiceProvider.GetRequiredService<IRecipientContacts>().Find(
                e.AssignedOfficerId is { } assigned ? [e.ApplicantId, assigned] : [e.ApplicantId],
                e.Kind == "STATUS" && e.Status == "SUBMITTED" && e.AssignedOfficerId is null ? e.WardCode : null, ct);
            foreach (var recipient in contacts.Where(r => r.UserId == e.ApplicantId || r.IsOfficer && r.WardCode == e.WardCode &&
                e.Kind == "STATUS" && e.Status is "SUBMITTED" or "UNDER_REVIEW" && (e.AssignedOfficerId == null || r.UserId == e.AssignedOfficerId)))
            {
                var notification = Create(e, recipient.UserId, recipient.UserId != e.ApplicantId);
                db.Notifications.Add(notification);
                db.Deliveries.Add(new() { NotificationId = notification.Id });
                if (configuration.GetValue<bool>("Email:Enabled")) db.Deliveries.Add(new() { NotificationId = notification.Id, Channel = "EMAIL" });
                if (configuration.GetValue<bool>("Sms:Enabled")) db.Deliveries.Add(new() { NotificationId = notification.Id, Channel = "SMS" });
            }
            row.State = "Processed"; row.ErrorCode = null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // No notification writes occur before directory lookup/validation succeeds.
            row.Attempts++; row.State = row.Attempts >= 10 ? "Dead" : "Pending";
            row.NextAttemptAt = RetrySchedule.Next(row.Attempts); row.ErrorCode = "notification.resolve_failed";
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Notifications:WorkersEnabled")) return;
        while (!ct.IsCancellationRequested)
        {
            try { if (!await ProcessOne(ct)) await Task.Delay(500, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Inbox chưa xử lý được; giữ dữ liệu để thử lại."); await Task.Delay(5000, ct); }
        }
    }
}
