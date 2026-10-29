namespace WardMate.Services.ApplicationWorkflow.Domain;

public static class ApplicationStates
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string UnderReview = "UNDER_REVIEW";
    public const string NeedRevision = "NEED_REVISION";
    public const string DocumentsReady = "DOCUMENTS_READY";
    public const string OfficiallyReceived = "OFFICIALLY_RECEIVED";
    public const string Cancelled = "CANCELLED";
}

public static class ChecklistStates
{
    public const string Pending = "PENDING";
    public const string Completed = "COMPLETED";
    public const string Rejected = "REJECTED";
}

public sealed class ApplicationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ApplicationCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid ProcedureId { get; set; }
    public string? WardCode { get; set; }
    public string ProcedureTitle { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? AssignedOfficerName { get; set; }
    // Snapshot of the applicant profile name at (re)submission; never CCCD or phone.
    public string? ApplicantName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public long AssignmentVersion { get; set; }
    public List<ApplicationAssignmentHistory> AssignmentHistory { get; set; } = [];
    public string? CaseCode { get; set; }
    public string Status { get; set; } = ApplicationStates.Draft;
    public string FormData { get; set; } = "{}";
    public DateTime? SubmittedAt { get; set; }
    public DateTime? FirstSubmittedAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public string DeadlineStatus { get; set; } = "UNKNOWN";
    public string? DeadlinePolicyCode { get; set; }
    public string? DeadlinePolicyVersion { get; set; }
    public string? DeadlineCalendarCode { get; set; }
    public string? DeadlineCalendarVersion { get; set; }
    public string? DeadlinePolicySnapshot { get; set; }
    public DateTime? DeadlinePausedAtUtc { get; set; }
    public string DeadlineHistory { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? AssignedOfficerId { get; set; }
    public int ResubmitCount { get; set; }
    public string? Notes { get; set; }
    // Historical value only; old decisions are not automatically readiness confirmations.
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DocumentsReadyAt { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public List<ApplicationComment> Comments { get; set; } = [];
    public List<ApplicationVersion> Versions { get; set; } = [];
    public List<ApplicationChecklist> Checklists { get; set; } = [];
    public List<ApplicationStatusHistory> History { get; set; } = [];

    public void SnapshotApplicantName(string? name)
    {
        var value = name?.Trim();
        if (!string.IsNullOrEmpty(value)) ApplicantName = value.Length > 255 ? value[..255] : value;
    }

    public void Submit(string code, Guid actorId, DateTime now)
    {
        if (Status != ApplicationStates.Draft || Checklists.Any(x => x.IsRequired && x.Status != ChecklistStates.Completed))
            throw new InvalidOperationException("Hồ sơ chưa đủ điều kiện nộp.");
        ApplicationCode = code;
        Status = ApplicationStates.Submitted;
        SubmittedAt = UpdatedAt = now;
        History.Add(new() { ApplicationId = Id, FromStatus = ApplicationStates.Draft,
            ToStatus = ApplicationStates.Submitted, ChangedBy = actorId, CreatedAt = now });
    }
}

public sealed class ApplicationChecklist
{
    public int SortOrder { get; set; }
    public string? CaseCode { get; set; }
    public string? SubmissionType { get; set; }
    public string? DocumentCopyType { get; set; }
    public int Quantity { get; set; } = 1;
    public string? ConditionNote { get; set; }
    public Guid? FormTemplateId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Status { get; set; } = ChecklistStates.Pending;
    public string? FileUrl { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ApplicationStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = ApplicationStates.Draft;
    public Guid ChangedBy { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}


