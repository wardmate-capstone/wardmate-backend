namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class ApplicationAssignmentHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string Action { get; set; } = "";
    public Guid? FromOfficerId { get; set; }
    public Guid? ToOfficerId { get; set; }
    public Guid ActorId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
