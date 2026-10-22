using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using WardMate.Services.AnalyticsSystem.Application;
namespace WardMate.Services.AnalyticsSystem.Infrastructure;

// PostgreSQL broadcasts to every replica; durable notification rows remain the reconnect source of truth.
public sealed class NotificationRealtimeListener(IServiceScopeFactory scopes, IConfiguration config, ILogger<NotificationRealtimeListener> logger) : BackgroundService
{
    public const string Channel = "wardmate_notification_changed";
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var settings = new NpgsqlConnectionStringBuilder(config.GetConnectionString("NotificationRealtimeDatabase") ?? config.GetConnectionString("NotificationsDatabase")) { Multiplexing = false, KeepAlive = 30 };
                await using var connection = new NpgsqlConnection(settings.ConnectionString);
                var pending = new ConcurrentDictionary<Guid, byte>();
                connection.Notification += (_, e) => { if (Guid.TryParse(e.Payload, out var id) && pending.Count < 10000) pending.TryAdd(id, 0); };
                await connection.OpenAsync(ct);
                await using (var command = new NpgsqlCommand("LISTEN " + Channel, connection)) await command.ExecuteNonQueryAsync(ct);
                while (!ct.IsCancellationRequested)
                {
                    await connection.WaitAsync(ct);
                    foreach (var id in pending.Keys)
                    {
                        pending.TryRemove(id, out _);
                        try { await Send(id, ct); }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception) { logger.LogWarning("Realtime signal failed for notification {NotificationId}; persisted list remains available.", id); }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Notification realtime listener disconnected; reconnecting. Clients can reload persisted notifications."); }
            try { await Task.Delay(5000, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
    private async Task Send(Guid id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var n = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        var guard = scope.ServiceProvider.GetRequiredService<NotificationConnectionGuard>();
        var connections = await guard.ValidatedConnections(n.RecipientUserId, n.RecipientWardCode, ct);
        if (connections.Length == 0) return;
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub, INotificationClient>>();
        var client = hub.Clients.Clients(connections);
        if (!n.IsRead) await client.ReceiveNotification(new(n.Id, n.RecipientUserId, n.ApplicationId, n.Title, n.Content, n.Type, n.Channel, n.IsRead, n.ReadAt, n.ActionUrl, n.CreatedAt));
        // Count per connection: ward access may differ from the notification's scope.
        foreach (var connectionId in connections)
        {
            var ward = guard.Ward(connectionId);
            var count = await db.Notifications.CountAsync(x => x.RecipientUserId == n.RecipientUserId && !x.IsRead && x.Channel == "IN_APP" && (x.RecipientWardCode == null || x.RecipientWardCode == ward), ct);
            await hub.Clients.Client(connectionId).UpdateUnreadCount(count);
        }
    }
}
