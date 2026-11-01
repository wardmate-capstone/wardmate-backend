using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.EventBus;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class WorkflowOutbox
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public static class WorkflowOutboxCapture
{
    public static void Capture(WorkflowDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var applications = db.ChangeTracker.Entries<ApplicationRecord>().ToDictionary(e => e.Entity.Id, e => e.Entity);
        var assignments = db.ChangeTracker.Entries<ApplicationAssignmentHistory>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        foreach (var assignment in assignments)
            if (applications.TryGetValue(assignment.ApplicationId, out var assignedApplication))
                Add(db, assignedApplication, assignment.Id, "APPLICATION_ASSIGNED", assignedApplication.Status, assignment.CreatedAt, assignment.FromOfficerId, assignment.Action);
        foreach (var h in db.ChangeTracker.Entries<ApplicationStatusHistory>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray())
            if (!assignments.Any(x => x.ApplicationId == h.ApplicationId) && h.ToStatus != ApplicationStates.Draft && applications.TryGetValue(h.ApplicationId, out var a))
                Add(db, a, h.Id, "STATUS", h.ToStatus, h.CreatedAt);
        foreach (var c in db.ChangeTracker.Entries<ApplicationComment>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray())
            if (applications.TryGetValue(c.ApplicationId, out var a)) Add(db, a, c.Id, "COMMENT", a.Status, c.CreatedAt);
    }
    private static void Add(WorkflowDbContext db, ApplicationRecord a, Guid id, string kind, string status, DateTime now, Guid? previousOfficerId = null, string? assignmentAction = null)
    {
        if (string.IsNullOrWhiteSpace(a.WardCode) || db.Outbox.Local.Any(x => x.Id == id)) return;
        var message = new WorkflowEvent(id, assignmentAction is null ? 1 : 2, a.Id, a.UserId, a.AssignedOfficerId, a.WardCode, string.IsNullOrWhiteSpace(a.ApplicationCode) ? a.Id.ToString("N") : a.ApplicationCode, kind, status, now, previousOfficerId, assignmentAction);
        db.Outbox.Add(new() { Id = id, Payload = JsonSerializer.Serialize(message), CreatedAt = now, NextAttemptAt = now });
    }
}

public sealed class WorkflowOutboxWorker(IServiceScopeFactory scopes, RabbitOptions options, RabbitStatus status, ILogger<WorkflowOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await new RabbitTransport(options).Connect(stoppingToken);
                await using var channel = await RabbitTransport.Open(connection, stoppingToken);
                status.Connected(connection);
                while (!stoppingToken.IsCancellationRequested)
                {
                    // No automatic recovery: reconnect as soon as the broker drops the connection, even while idle.
                    if (!connection.IsOpen) { status.Disconnected(); break; }
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
                    await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                    var row = await db.Outbox.FromSqlRaw("SELECT * FROM workflow_outbox WHERE published_at IS NULL AND next_attempt_at <= now() ORDER BY created_at, id LIMIT 1 FOR UPDATE SKIP LOCKED")
                        .SingleOrDefaultAsync(stoppingToken);
                    if (row is null) { await transaction.RollbackAsync(stoppingToken); await Task.Delay(500, stoppingToken); continue; }
                    try
                    {
                        await RabbitTransport.Publish(channel, JsonSerializer.Deserialize<WorkflowEvent>(row.Payload)!, stoppingToken);
                        row.PublishedAt = DateTime.UtcNow; row.LastError = null;
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        row.Attempts++; row.NextAttemptAt = RetrySchedule.Next(row.Attempts); row.LastError = "broker.publish_failed";
                        await db.SaveChangesAsync(stoppingToken); await transaction.CommitAsync(stoppingToken); throw;
                    }
                    await db.SaveChangesAsync(stoppingToken); await transaction.CommitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { status.Disconnected(); logger.LogWarning("Outbox chưa kết nối/gửi được RabbitMQ; dữ liệu giữ lại để thử lại."); await Task.Delay(5000, stoppingToken); }
        }
    }
}
