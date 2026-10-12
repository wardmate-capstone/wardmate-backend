using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.EventBus;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class WorkflowNotificationConsumer(IServiceScopeFactory scopes, RabbitOptions options,
    ILogger<WorkflowNotificationConsumer> logger) : BackgroundService
{
    public static bool Valid(WorkflowEvent? e) => e is { Version: 1 } && e.EventId != Guid.Empty && e.ApplicationId != Guid.Empty &&
        e.ApplicantId != Guid.Empty && e.AssignedOfficerId != Guid.Empty && e.OccurredAt != default && e.OccurredAt.Kind == DateTimeKind.Utc && !string.IsNullOrWhiteSpace(e.WardCode) && e.WardCode.Length <= 50 &&
        !string.IsNullOrWhiteSpace(e.ApplicationCode) && e.ApplicationCode.Length <= 50 && e.Kind is "STATUS" or "COMMENT" &&
        e.Status is "SUBMITTED" or "UNDER_REVIEW" or "NEED_REVISION" or "APPROVED" or "REJECTED" or "CANCELLED";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var connection = await new RabbitTransport(options).Connect(ct);
                await using var channel = await RabbitTransport.Open(connection, ct);
                while (!ct.IsCancellationRequested)
                {
                    var delivery = await channel.BasicGetAsync(RabbitTransport.Queue, false, ct);
                    if (delivery is null) { await Task.Delay(250, ct); continue; }
                    WorkflowEvent? message = null;
                    try { if (delivery.Body.Length <= 65536) message = JsonSerializer.Deserialize<WorkflowEvent>(delivery.Body.Span); }
                    catch (JsonException) { }
                    if (!Valid(message))
                    {
                        await channel.BasicRejectAsync(delivery.DeliveryTag, false, ct);
                        logger.LogWarning("Sự kiện Workflow không hợp lệ đã chuyển dead-letter queue.");
                        continue;
                    }
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                    var payload = JsonSerializer.Serialize(message);
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO notification_inbox (\"Id\", \"Payload\", \"State\", \"Attempts\", \"NextAttemptAt\", \"CreatedAt\") VALUES ({message!.EventId}, CAST({payload} AS jsonb), 'Pending', 0, now(), now()) ON CONFLICT (\"Id\") DO NOTHING", ct);
                    await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Consumer RabbitMQ tạm lỗi; message chưa ACK sẽ được giao lại."); await Task.Delay(5000, ct); }
        }
    }
}
