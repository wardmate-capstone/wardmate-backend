namespace WardMate.Services.AnalyticsSystem.Domain;

public sealed class BusinessAuditEvent
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "";
    public string Entity { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid? ActorId { get; set; }
    public string? TraceId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string Payload { get; set; } = "";
    public string PayloadHash { get; set; } = "";
}
