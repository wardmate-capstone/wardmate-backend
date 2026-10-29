namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class AssignmentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid? FromOfficerId { get; set; }
    public Guid ToOfficerId { get; set; }
    public Guid ActorId { get; set; }
    public long Version { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = string.Empty;
}
