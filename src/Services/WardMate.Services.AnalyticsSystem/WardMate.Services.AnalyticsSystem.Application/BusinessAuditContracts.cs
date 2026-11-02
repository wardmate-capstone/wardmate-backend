using System.Text.Json;

namespace WardMate.Services.AnalyticsSystem.Application;

public sealed record BusinessAuditMessage(Guid EventId, string Source, string Schema, string Entity,
    JsonElement EntityKey, string Action, JsonElement Before, JsonElement After,
    Guid? ActorId, string? ClientIp, string? TraceId, DateTimeOffset OccurredAtUtc);
public sealed record BusinessAuditQuery(DateTime? From = null, DateTime? To = null, string? Source = null,
    string? Entity = null, Guid? ActorId = null, string? TraceId = null, int Page = 1, int PageSize = 20);
public interface IBusinessAuditStore
{
    Task Receive(string source, string body, CancellationToken ct);
    Task<AnalyticsPage<BusinessAuditMessage>> List(BusinessAuditQuery query, CancellationToken ct);
    Task<BusinessAuditMessage> Get(Guid id, CancellationToken ct);
}
