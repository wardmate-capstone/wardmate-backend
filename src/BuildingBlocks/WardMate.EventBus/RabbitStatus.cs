using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace WardMate.EventBus;

// Connection reported by the service's RabbitMQ worker. The health check reads it instead of opening a new broker
// connection on every probe (CloudAMQP free plan limits connections). An idle worker does not notice a broker that went
// away, so the live IsOpen flag is read rather than the last state the worker saw.
public sealed class RabbitStatus
{
    private volatile IConnection? connection;
    private volatile bool failed;
    public void Connected(IConnection value) { connection = value; failed = false; }
    public void Disconnected() { connection = null; failed = true; }
    public bool IsOpen => connection?.IsOpen == true;
    public bool Failed => failed || connection is { IsOpen: false };
}

public sealed class RabbitHealthCheck(RabbitStatus status, RabbitOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) => Task.FromResult(
        !options.Enabled ? HealthCheckResult.Healthy("disabled")
        : status.IsOpen ? HealthCheckResult.Healthy("connected")
        : status.Failed ? HealthCheckResult.Unhealthy("disconnected")
        : HealthCheckResult.Degraded("connecting"));
}
