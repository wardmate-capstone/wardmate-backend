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
    // Display-name snapshots taken when the row is written; null for rows written before 2026-11-08.
    public string? FromOfficerName { get; set; }
    public string? ToOfficerName { get; set; }
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; }
}
