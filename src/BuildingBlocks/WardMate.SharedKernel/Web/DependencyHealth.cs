using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WardMate.SharedKernel.Web;

// /health stays a liveness probe with no dependencies, so a database or broker outage never makes the platform restart
// the container. /health/ready runs the checks tagged "ready" and answers 503 when one is unhealthy.
public static class DependencyHealth
{
    public const string ReadyTag = "ready";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // probe returns true when the dependency answers. Exceptions and timeouts are reported as unhealthy
    // without their messages, which can contain host names or connection details.
    public static IHealthChecksBuilder AddReadyProbe<TService>(this IHealthChecksBuilder builder, string name,
        Func<TService, CancellationToken, Task<bool>> probe) where TService : notnull =>
        builder.Add(new HealthCheckRegistration(name, sp => new ProbeHealthCheck<TService>(sp.GetRequiredService<IServiceScopeFactory>(), probe),
            HealthStatus.Unhealthy, [ReadyTag], Timeout));

    public static IEndpointRouteBuilder MapWardMateHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(ReadyTag),
            ResultStatusCodes = { [HealthStatus.Healthy] = 200, [HealthStatus.Degraded] = 200, [HealthStatus.Unhealthy] = 503 },
            ResponseWriter = Write
        });
        return app;
    }

    private static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            durationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key, status = e.Value.Status.ToString(),
                description = e.Value.Description, durationMs = (int)e.Value.Duration.TotalMilliseconds
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private sealed class ProbeHealthCheck<TService>(IServiceScopeFactory scopes, Func<TService, CancellationToken, Task<bool>> probe)
        : IHealthCheck where TService : notnull
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                return await probe(scope.ServiceProvider.GetRequiredService<TService>(), ct)
                    ? HealthCheckResult.Healthy("reachable") : HealthCheckResult.Unhealthy("unreachable");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return HealthCheckResult.Unhealthy("timeout"); }
            catch (Exception e) when (e is not OperationCanceledException) { return HealthCheckResult.Unhealthy("error: " + e.GetType().Name); }
        }
    }
}
