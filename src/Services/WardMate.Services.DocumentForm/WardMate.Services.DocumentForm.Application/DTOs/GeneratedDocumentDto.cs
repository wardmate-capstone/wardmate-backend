namespace WardMate.Services.DocumentForm.Application.DTOs;

public sealed record GeneratedDocumentDto
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid? SubmissionId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string PdfBlobUrl { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
