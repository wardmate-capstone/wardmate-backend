namespace WardMate.Services.AnalyticsSystem.Domain;

public sealed class SystemSetting
{
    public string Key { get; set; } = "";
    public int Value { get; set; }
    public long Version { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class SystemAuditLog
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string EntityKey { get; set; } = "";
    public int? PreviousValue { get; set; }
    public int NewValue { get; set; }
    public long Version { get; set; }
    public string Reason { get; set; } = "";
    public string? ClientIp { get; set; }
    public string TraceId { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
}
