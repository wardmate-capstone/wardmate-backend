using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class SystemHealth(AnalyticsDbContext db, IConfiguration config) : ISystemHealth
{
    public async Task<Guid> RequestCheck(Guid actorId, CancellationToken ct)
    {
        Enabled();
        if (actorId == Guid.Empty) throw new AnalyticsException(401, "auth.unauthorized", "Vui lòng đăng nhập.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(353535, 2)", ct);
        var existing = await db.HealthCheckRequests.Where(x => x.CompletedAtUtc == null)
            .OrderBy(x => x.RequestedAtUtc).FirstOrDefaultAsync(ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return existing.Id; }
        var request = new HealthCheckRequest { Id = Guid.NewGuid(), RequestedBy = actorId, RequestedAtUtc = DateTime.UtcNow };
        db.HealthCheckRequests.Add(request);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return request.Id;
    }
    private void Enabled()
    {
        if (!config.GetValue("SystemHealth:Enabled", false))
            throw new AnalyticsException(503, "system.health_disabled", "Chưa bật theo dõi sức khỏe hệ thống.");
    }
    public async Task<HealthObservation[]> Latest(CancellationToken ct)
    {
        Enabled();
        var result = new List<HealthObservation>();
        foreach (var target in config.GetSection("SystemHealth:Targets").GetChildren())
        {
            var row = await db.ServiceHealthLogs.AsNoTracking().Where(x => x.ServiceName == target.Key)
                .OrderByDescending(x => x.CheckedAtUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            result.Add(row is null ? new(Guid.Empty, target.Key, "Unknown", null, 0, "not_checked", default)
                : row.CheckedAtUtc < DateTime.UtcNow.AddSeconds(-2 * config.GetValue("SystemHealth:IntervalSeconds", 300))
                ? Map(row) with { Status = "Unknown", ErrorCode = "stale_observation" } : Map(row));
        }
        return result.ToArray();
    }
    public async Task<AnalyticsPage<HealthObservation>> History(string? service, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct)
    {
        Enabled();
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100 || from > to ||
            from is { Kind: not DateTimeKind.Utc } || to is { Kind: not DateTimeKind.Utc })
            throw new AnalyticsException(400, "system.invalid_query", "Dùng UTC có Z, from <= to và phân trang hợp lệ.");
        var q = db.ServiceHealthLogs.AsNoTracking().AsQueryable();
        if (service is not null) q = q.Where(x => x.ServiceName == service);
        if (from.HasValue) q = q.Where(x => x.CheckedAtUtc >= from);
        if (to.HasValue) q = q.Where(x => x.CheckedAtUtc <= to);
        var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CheckedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(Map).ToArray(), page, pageSize, count);
    }
    private static HealthObservation Map(ServiceHealthLog x) => new(x.Id, x.ServiceName, x.Status,
        x.HttpStatus, x.ResponseTimeMs, x.ErrorCode, x.CheckedAtUtc);
}

public sealed class SystemHealthWorker(IServiceScopeFactory scopes, IHttpClientFactory clients,
    IConfiguration config, ILogger<SystemHealthWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("SystemHealth:Enabled", false)) return;
        var interval = config.GetValue("SystemHealth:IntervalSeconds", 300);
        if (interval is < 30 or > 3600) throw new InvalidOperationException("SystemHealth interval must be 30..3600 seconds.");
        var targets = config.GetSection("SystemHealth:Targets").GetChildren().Select(x => (Name: x.Key, Url: x.Value)).ToArray();
        if (targets.Length is < 1 or > 20) throw new InvalidOperationException("Configure 1..20 health targets.");
        foreach (var target in targets)
            if (target.Name.Length > 64 || !Uri.TryCreate(target.Url, UriKind.Absolute, out var uri) ||
                !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new InvalidOperationException("Health target must be HTTPS or loopback HTTP, without credentials/query.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                // Serializes collectors across replicas; transaction-scoped lock is released on failure.
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(353535, 1)", stoppingToken);
                var newest = await db.ServiceHealthLogs.MaxAsync(x => (DateTime?)x.CheckedAtUtc, stoppingToken);
                var pending = await db.HealthCheckRequests.Where(x => x.CompletedAtUtc == null).ToArrayAsync(stoppingToken);
                if (pending.Length > 0 || !newest.HasValue || newest < DateTime.UtcNow.AddSeconds(-interval))
                {
                    foreach (var target in targets)
                    {
                        var watch = Stopwatch.StartNew();
                        var row = new ServiceHealthLog { Id = Guid.NewGuid(), ServiceName = target.Name, Status = "Unhealthy" };
                        try
                        {
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                            timeout.CancelAfter(TimeSpan.FromSeconds(5));
                            using var response = await clients.CreateClient("system-health").GetAsync(target.Url, timeout.Token);
                            row.HttpStatus = (int)response.StatusCode;
                            if (response.IsSuccessStatusCode)
                            {
                                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                                if (json.RootElement.TryGetProperty("status", out var status) &&
                                    status.GetString() is "Healthy" or "Degraded" or "Unhealthy") row.Status = status.GetString()!;
                                else row.ErrorCode = "invalid_response";
                            }
                            else row.ErrorCode = "dependency_unhealthy";
                        }
                        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { row.ErrorCode = "timeout"; }
                        catch (HttpRequestException) { row.ErrorCode = "unreachable"; }
                        catch (JsonException) { row.ErrorCode = "invalid_response"; }
                        row.ResponseTimeMs = watch.ElapsedMilliseconds;
                        row.CheckedAtUtc = DateTime.UtcNow;
                        db.ServiceHealthLogs.Add(row);
                    }
                    foreach (var request in pending) request.CompletedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
                await transaction.CommitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Health collection failed: {ErrorType}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
