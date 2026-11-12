using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationDeliveryWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Deliveries.FromSqlRaw("SELECT * FROM notification_deliveries WHERE \"State\" = 'Pending' AND \"NextAttemptAt\" <= now() ORDER BY \"NextAttemptAt\", \"Id\" LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if (row is null) return false;
        // SMS was removed; a pending SMS row left from before is closed without sending.
        if (row.Channel == "SMS")
        {
            row.State = "Suppressed"; row.ErrorCode = "sms.removed";
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        await ProcessDelivery();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;

        async Task ProcessDelivery()
        {
        try
        {
            var n = await db.Notifications.SingleAsync(x => x.Id == row.NotificationId, ct);
            var contact = (await scope.ServiceProvider.GetRequiredService<IRecipientContacts>().Find([n.RecipientUserId], null, ct)).SingleOrDefault();
            if (contact is null || n.RecipientWardCode != null && (!contact.IsOfficer || contact.WardCode != n.RecipientWardCode)) row.State = "Suppressed";
            else if (row.Channel == "IN_APP")
            {
                // Transactional NOTIFY reaches listeners on all replicas only after this transaction commits.
                var payload = n.Id.ToString();
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_notify({NotificationRealtimeListener.Channel}, {payload})", ct);
                row.State = "Sent";
                // PUSH-01: thông báo trong app đã hợp lệ cho người nhận → xếp thêm một lần gửi đẩy tới điện thoại (nếu bật).
                if (ShouldQueuePush(configuration, n) && !await db.Deliveries.AnyAsync(x => x.NotificationId == n.Id && x.Channel == "PUSH", ct))
                    db.Deliveries.Add(new() { NotificationId = n.Id, Channel = "PUSH" });
            }
            else if (row.Channel == "PUSH")
                row.State = await scope.ServiceProvider.GetRequiredService<PushDeliverySender>().Send(n, ct);
            else row.State = await scope.ServiceProvider.GetRequiredService<ExternalNotificationSender>().Send(n, contact, row, ct);
            row.SentAt = row.State == "Sent" ? DateTime.UtcNow : null;
            row.ErrorCode = row.State == "Blocked" ? "notification.channel_not_configured" : null;
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            row.Attempts++; row.State = row.Attempts >= 10 ? "Dead" : "Pending";
            row.NextAttemptAt = RetrySchedule.Next(row.Attempts); row.ErrorCode = error is NotificationSendException failure ? failure.Code : "notification.delivery_failed";
            logger.LogWarning("Notification delivery {DeliveryId} failed; attempt {Attempt}, state {State}.", row.Id, row.Attempts, row.State);
        }
        }
    }
    /// <summary>Chỉ đẩy thông báo trong app khi Push:Enabled; thông báo cũ hơn 1 ngày (gửi lại muộn) không đẩy để tránh làm phiền.</summary>
    public static bool ShouldQueuePush(IConfiguration configuration, WardMate.Services.AnalyticsSystem.Domain.Notification n) =>
        configuration.GetValue<bool>("Push:Enabled") && n.Channel == "IN_APP" && n.CreatedAt > DateTime.UtcNow.AddDays(-1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Notifications:WorkersEnabled")) return;
        while (!ct.IsCancellationRequested)
        {
            try { if (!await ProcessOne(ct)) await Task.Delay(500, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Hàng đợi gửi thông báo tạm lỗi; giữ dữ liệu để thử lại."); await Task.Delay(5000, ct); }
        }
    }
}
