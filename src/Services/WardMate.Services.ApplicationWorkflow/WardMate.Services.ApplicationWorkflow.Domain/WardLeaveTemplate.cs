namespace WardMate.Services.ApplicationWorkflow.Domain;

public sealed class WardLeaveTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WardCode { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public string FileName { get; set; } = "don-nghi.docx";
    public Guid UploadedBy { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
