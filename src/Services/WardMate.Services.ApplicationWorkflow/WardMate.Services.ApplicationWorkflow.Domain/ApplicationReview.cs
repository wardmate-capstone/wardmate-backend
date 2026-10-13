namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class ApplicationVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public int VersionNumber { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string SnapshotData { get; set; } = "{}";
}

public sealed class ApplicationComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid ApplicationVersionId { get; set; }
    public Guid OfficerId { get; set; }
    public string TargetType { get; set; } = "FORM_FIELD";
    public string TargetId { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public string Status { get; set; } = "OPEN";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
