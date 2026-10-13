namespace WardMate.Services.ApplicationWorkflow.Domain;

public static class ApplicationStates
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string UnderReview = "UNDER_REVIEW";
    public const string NeedRevision = "NEED_REVISION";
    public const string Rejected = "REJECTED";
    public const string Approved = "APPROVED";
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
    public string? CaseCode { get; set; }
    public string Status { get; set; } = ApplicationStates.Draft;
    public string FormData { get; set; } = "{}";
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? AssignedOfficerId { get; set; }
    public int ResubmitCount { get; set; }
    public string? Notes { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<ApplicationComment> Comments { get; set; } = [];
    public List<ApplicationVersion> Versions { get; set; } = [];
    public List<ApplicationChecklist> Checklists { get; set; } = [];
    public List<ApplicationStatusHistory> History { get; set; } = [];

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


