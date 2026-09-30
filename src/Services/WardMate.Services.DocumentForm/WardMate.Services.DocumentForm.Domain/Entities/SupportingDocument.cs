using WardMate.SharedKernel.Domain;

namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// Đại diện cho tài liệu đính kèm (giấy tờ minh chứng tải lên) của hồ sơ.
/// Bảng: document.supporting_documents
/// </summary>
public sealed class SupportingDocument : BaseEntity
{
    private SupportingDocument()
    {
    }

    public SupportingDocument(
        Guid applicationId,
        string fileName,
        string blobUrl,
        Guid? checklistId = null,
        long? fileSizeBytes = null,
        string? contentType = null,
        string? createdBy = null)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("Application ID cannot be empty.", nameof(applicationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUrl);

        ApplicationId = applicationId;
        ChecklistId = checklistId;
        FileName = fileName.Trim();
        BlobUrl = blobUrl.Trim();
        FileSizeBytes = fileSizeBytes;
        ContentType = contentType?.Trim();
        CreatedBy = createdBy;
        SetCreated(DateTime.UtcNow);
    }

    public Guid ApplicationId { get; private set; }
    public Guid? ChecklistId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string BlobUrl { get; private set; } = string.Empty;
    public long? FileSizeBytes { get; private set; }
    public string? ContentType { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public void UpdateFileDetails(string fileName, string blobUrl, long? fileSizeBytes, string? contentType, string? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUrl);

        FileName = fileName.Trim();
        BlobUrl = blobUrl.Trim();
        FileSizeBytes = fileSizeBytes;
        ContentType = contentType?.Trim();
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }
}
