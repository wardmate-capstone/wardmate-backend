namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class LeaveDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfficerId { get; set; }
    public string WardCode { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public Guid TemplateVersionId { get; set; }
    public string TemplateHash { get; set; } = "";
    public int Version { get; set; } = 1;
    public int MetadataVersion { get; set; } = 1;
    public Guid CreationKey { get; set; }
    public string CreationHash { get; set; } = "";
    public Guid? CurrentRevisionId { get; set; }
    public Guid? SubmittedLeaveId { get; set; }
    public string? SubmittedOfficerName { get; set; }
    public Guid? SubmissionKey { get; set; }
    public string? SubmissionHash { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public sealed class LeaveDraftAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraftId { get; set; }
    public Guid ActorId { get; set; }
    public string WardCode { get; set; } = "";
    public string Action { get; set; } = "";
    public int Version { get; set; }
    public Guid? RevisionId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public sealed class LeaveDocumentRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraftId { get; set; }
    public int MetadataVersion { get; set; }
    public string Sha256 { get; set; } = "";
    public int SizeBytes { get; set; }
    public DateTime SavedAtUtc { get; set; }
}
