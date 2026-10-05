using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Domain;

namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// Đại diện cho một hồ sơ (bản khai) mà người dùng tạo ra từ một phôi mẫu (FormTemplate).
/// Hỗ trợ lưu nháp nhiều lần, nộp hồ sơ, cán bộ gửi comment yêu cầu sửa và duyệt.
/// Bảng: document.user_submissions
/// </summary>
public sealed class UserSubmission : BaseEntity
{
    private UserSubmission()
    {
    }

    /// <summary>
    /// Tạo mới một bản nháp hồ sơ từ phôi mẫu.
    /// </summary>
    public UserSubmission(
        Guid templateId,
        Guid applicantId,
        string blobUrl,
        string fileName,
        long fileSizeBytes,
        string? createdBy = null)
    {
        if (templateId == Guid.Empty)
            throw new ArgumentException("Template ID cannot be empty.", nameof(templateId));
        if (applicantId == Guid.Empty)
            throw new ArgumentException("Applicant ID cannot be empty.", nameof(applicantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        TemplateId = templateId;
        ApplicantId = applicantId;
        BlobUrl = blobUrl.Trim();
        FileName = fileName.Trim();
        FileSizeBytes = fileSizeBytes;
        Status = SubmissionStatus.Draft;
        CreatedBy = createdBy;
        SetCreated(DateTime.UtcNow);
    }

    // ── Properties ──────────────────────────────────────────────────

    /// <summary>ID phôi mẫu gốc mà hồ sơ này được tạo từ đó.</summary>
    public Guid TemplateId { get; private set; }

    /// <summary>ID người dân sở hữu hồ sơ này.</summary>
    public Guid ApplicantId { get; private set; }

    /// <summary>URL blob của file DOCX đang lưu nháp hoặc đã nộp.</summary>
    public string BlobUrl { get; private set; } = string.Empty;

    /// <summary>Tên file gốc.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Kích thước file (bytes).</summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>Trạng thái hiện tại của hồ sơ.</summary>
    public SubmissionStatus Status { get; private set; } = SubmissionStatus.Draft;

    /// <summary>Comment của cán bộ khi yêu cầu sửa lại (null nếu không có).</summary>
    public string? OfficerComment { get; private set; }

    /// <summary>ID cán bộ xử lý (null nếu chưa có cán bộ nào xem).</summary>
    public Guid? ReviewedByOfficerId { get; private set; }

    /// <summary>Thời điểm nộp hồ sơ chính thức.</summary>
    public DateTime? SubmittedAt { get; private set; }

    /// <summary>Thời điểm cán bộ duyệt hoặc trả lại hồ sơ.</summary>
    public DateTime? ReviewedAt { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // ── Behaviour Methods ────────────────────────────────────────────

    /// <summary>
    /// Cập nhật file nháp (người dùng lưu tiếp sau khi điền thêm).
    /// Chỉ cho phép khi hồ sơ đang ở trạng thái Draft hoặc RevisionRequested.
    /// </summary>
    public void SaveDraft(string blobUrl, string fileName, long fileSizeBytes, string? updatedBy = null)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.RevisionRequested)
            throw new InvalidOperationException($"Cannot save draft when submission status is '{Status}'.");

        ArgumentException.ThrowIfNullOrWhiteSpace(blobUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        BlobUrl = blobUrl.Trim();
        FileName = fileName.Trim();
        FileSizeBytes = fileSizeBytes;

        // Khi người dùng sửa lại sau lần bị trả về, reset status về Draft
        if (Status == SubmissionStatus.RevisionRequested)
            Status = SubmissionStatus.Draft;

        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }

    /// <summary>
    /// Người dùng nộp hồ sơ chính thức (chuyển Draft -> Submitted).
    /// </summary>
    public void Submit(string? updatedBy = null)
    {
        if (Status != SubmissionStatus.Draft)
            throw new InvalidOperationException($"Cannot submit when submission status is '{Status}'.");

        Status = SubmissionStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        OfficerComment = null;
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }

    /// <summary>
    /// Cán bộ yêu cầu sửa lại, kèm comment hướng dẫn.
    /// </summary>
    public void RequestRevision(Guid officerId, string comment, string? updatedBy = null)
    {
        if (Status != SubmissionStatus.Submitted)
            throw new InvalidOperationException($"Cannot request revision when submission status is '{Status}'.");

        if (officerId == Guid.Empty)
            throw new ArgumentException("Officer ID cannot be empty.", nameof(officerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);

        Status = SubmissionStatus.RevisionRequested;
        OfficerComment = comment.Trim();
        ReviewedByOfficerId = officerId;
        ReviewedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }

    /// <summary>
    /// Cán bộ duyệt hồ sơ (chuyển Submitted -> Approved).
    /// </summary>
    public void Approve(Guid officerId, string? updatedBy = null)
    {
        if (Status != SubmissionStatus.Submitted)
            throw new InvalidOperationException($"Cannot approve when submission status is '{Status}'.");

        if (officerId == Guid.Empty)
            throw new ArgumentException("Officer ID cannot be empty.", nameof(officerId));

        Status = SubmissionStatus.Approved;
        ReviewedByOfficerId = officerId;
        ReviewedAt = DateTime.UtcNow;
        OfficerComment = null;
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }
}
