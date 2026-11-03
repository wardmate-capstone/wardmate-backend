namespace WardMate.Services.AnalyticsSystem.Application;

public sealed record HealthObservation(Guid Id, string ServiceName, string Status, int? HttpStatus,
    long ResponseTimeMs, string? ErrorCode, DateTime CheckedAtUtc);
public interface ISystemHealth
{
    Task<Guid> RequestCheck(Guid actorId, CancellationToken ct);
    Task<HealthObservation[]> Latest(CancellationToken ct);
    Task<AnalyticsPage<HealthObservation>> History(string? service, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct);
}
