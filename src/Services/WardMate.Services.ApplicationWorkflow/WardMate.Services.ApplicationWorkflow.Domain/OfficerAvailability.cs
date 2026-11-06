namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class OfficerShift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfficerId { get; set; }
    public string WardCode { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
}

public sealed class OfficerLeave
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfficerId { get; set; }
    public string WardCode { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
}

public sealed class OfficerLeaveDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeaveId { get; set; }
    public Guid ActorId { get; set; }
    public int Version { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
