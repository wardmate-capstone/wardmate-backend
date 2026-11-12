namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class AssignmentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid? FromOfficerId { get; set; }
    public Guid ToOfficerId { get; set; }
    public Guid ActorId { get; set; }
    public long Version { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FromOfficerName { get; set; }
    public string? ToOfficerName { get; set; }
    public string? ActorName { get; set; }
}
