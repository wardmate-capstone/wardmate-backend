using WardMate.SharedKernel.Domain;

namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// Đại diện cho văn bản/giấy tờ kết quả được hệ thống tự động sinh ra (PDF/DOCX kết xuất).
/// Bảng: document.generated_documents
/// </summary>
public sealed class GeneratedDocument : BaseEntity
{
    private GeneratedDocument()
    {
    }

    public GeneratedDocument(
        Guid applicationId,
        string documentType,
        string pdfBlobUrl,
        string? createdBy = null)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("Application ID cannot be empty.", nameof(applicationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfBlobUrl);

        ApplicationId = applicationId;
        DocumentType = documentType.Trim().ToUpperInvariant();
        PdfBlobUrl = pdfBlobUrl.Trim();
        CreatedBy = createdBy;
        SetCreated();
    }

    public Guid ApplicationId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string PdfBlobUrl { get; private set; } = string.Empty;
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public void UpdateBlobUrl(string pdfBlobUrl, string? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfBlobUrl);
        PdfBlobUrl = pdfBlobUrl.Trim();
        UpdatedBy = updatedBy;
        SetUpdated();
    }
}
