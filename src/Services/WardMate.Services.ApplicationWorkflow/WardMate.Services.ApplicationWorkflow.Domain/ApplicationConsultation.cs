namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class ApplicationConsultation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid SpecialistId { get; set; }
    public Guid RequestedBy { get; set; }
    public int ApplicationVersion { get; set; }
    public long AssignmentVersion { get; set; }
    public string Question { get; set; } = "";
    public string? Answer { get; set; }
    public string Status { get; set; } = "OPEN";
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? AnsweredAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedBy { get; set; }
    public string? CloseReason { get; set; }
    // Display snapshots (2026-11-08); null on rows created earlier.
    public string? ApplicationCode { get; set; }
    public string? SpecialistName { get; set; }
    public string? RequestedByName { get; set; }
    public string? ClosedByName { get; set; }
}
