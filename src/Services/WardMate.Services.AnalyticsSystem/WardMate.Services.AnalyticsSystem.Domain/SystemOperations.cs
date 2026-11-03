namespace WardMate.Services.AnalyticsSystem.Domain;

public sealed class HealthCheckRequest
{
    public Guid Id { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

// Historical observations; Unknown is distinct from a successful probe.
public sealed class ServiceHealthLog
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    public int? HttpStatus { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CheckedAtUtc { get; set; }
}

// Verified means archive integrity was checked, not that a restore drill succeeded.
public sealed class SystemBackupLog
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = "";
    public string Status { get; set; } = "Queued";
    public string? StorageObjectName { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public Guid? RequestedBy { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime? RestoreTestedAtUtc { get; set; }
    public string? ErrorCode { get; set; }
}
