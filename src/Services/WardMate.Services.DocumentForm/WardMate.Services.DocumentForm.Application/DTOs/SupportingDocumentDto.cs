namespace WardMate.Services.DocumentForm.Application.DTOs;

public sealed record SupportingDocumentDto
{
    public string? SubmissionType { get; init; }
    public string? DocumentCopyType { get; init; }
    public int Quantity { get; init; } = 1;
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid? ChecklistId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string BlobUrl { get; init; } = string.Empty;
    public long? FileSizeBytes { get; init; }
    public string? ContentType { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
