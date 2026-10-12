namespace WardMate.Services.DocumentForm.Application.DTOs;

public sealed record SupportingDocumentDto
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid? ChecklistId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string BlobUrl { get; init; } = string.Empty;
    public long? FileSizeBytes { get; init; }
    public string? ContentType { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
