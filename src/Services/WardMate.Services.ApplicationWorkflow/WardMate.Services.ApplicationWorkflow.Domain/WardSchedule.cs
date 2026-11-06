namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class ScheduleRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WardCode { get; set; } = "";
    public DateOnly Month { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "DRAFT";
    public string BaseFingerprint { get; set; } = "";
    public string SlotsJson { get; set; } = "[]";
    public Guid ActorId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime? PublishedAtUtc { get; set; }
}
public sealed class WeekPattern
{
    public string WardCode { get; set; } = "";
    public int Version { get; set; }
    public string EntriesJson { get; set; } = "[]";
}
public sealed class OfficerScheduleReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WardCode { get; set; } = "";
    public DateOnly Month { get; set; }
    public Guid OfficerId { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTime ConfirmedAtUtc { get; set; }
}
public sealed class ShiftChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public Guid OfficerId { get; set; }
    public string WardCode { get; set; } = "";
    public DateTime OriginalStart { get; set; }
    public DateTime OriginalEnd { get; set; }
    public int OriginalVersion { get; set; }
    public DateTime ProposedStart { get; set; }
    public DateTime ProposedEnd { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
}
