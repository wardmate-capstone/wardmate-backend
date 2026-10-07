namespace WardMate.Services.ProcedureCatalog.Domain.Entities;

public sealed class ProcedureDraft
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Queued";
    public string BlobName { get; set; } = string.Empty;
    public string PdfFileName { get; set; } = string.Empty;
    public string OriginalPdfUrl { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string WarningsJson { get; set; } = "[]";
    public string ExtractedText { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public Guid? PublishedProcedureId { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
