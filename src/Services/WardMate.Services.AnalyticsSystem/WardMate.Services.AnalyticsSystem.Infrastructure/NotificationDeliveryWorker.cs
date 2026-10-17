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
        try
        {
            var n = await db.Notifications.SingleAsync(x => x.Id == row.NotificationId, ct);
            var contact = (await scope.ServiceProvider.GetRequiredService<IRecipientContacts>().Find([n.RecipientUserId], null, ct)).SingleOrDefault();
            if (contact is null || n.RecipientWardCode != null && (!contact.IsOfficer || contact.WardCode != n.RecipientWardCode)) row.State = "Suppressed";
            else if (row.Channel == "IN_APP")
            {
                var guard = scope.ServiceProvider.GetRequiredService<NotificationConnectionGuard>();
                var connections = await guard.ValidatedConnections(n.RecipientUserId, n.RecipientWardCode, ct);
                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub, INotificationClient>>();
                var client = hub.Clients.Clients(connections);
                await client.ReceiveNotification(new(n.Id, n.RecipientUserId, n.ApplicationId, n.Title, n.Content, n.Type, n.Channel, n.IsRead, n.ReadAt, n.ActionUrl, n.CreatedAt));
                await client.UpdateUnreadCount(await db.Notifications.CountAsync(x => x.RecipientUserId == n.RecipientUserId && !x.IsRead && x.Channel == "IN_APP" &&
                    (x.RecipientWardCode == null || contact.IsOfficer && x.RecipientWardCode == contact.WardCode), ct));
                row.State = "Sent";
            }
            else row.State = await scope.ServiceProvider.GetRequiredService<ExternalNotificationSender>().Send(n, contact, row, ct);
            row.SentAt = row.State == "Sent" ? DateTime.UtcNow : null;
            row.ErrorCode = row.State == "Blocked" ? "notification.channel_not_configured" : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            row.Attempts++; row.State = row.Attempts >= 10 ? "Dead" : "Pending";
            row.NextAttemptAt = RetrySchedule.Next(row.Attempts); row.ErrorCode = "notification.delivery_failed";
            logger.LogWarning("Notification delivery {DeliveryId} failed; attempt {Attempt}, state {State}.", row.Id, row.Attempts, row.State);
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
            catch (Exception) { logger.LogWarning("Hàng đợi gửi thông báo tạm lỗi; giữ dữ liệu để thử lại."); await Task.Delay(5000, ct); }
        }
    }
}
