namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// DOC-FILEVER-01: tệp (tờ khai DOCX hoặc giấy tờ tải lên) của hồ sơ tại một lần gửi. Chụp ngay trước khi Workflow tạo phiên bản
/// hồ sơ; blob của các lần gửi trước không bị xóa khi công dân sửa, nên cán bộ mở lại được bản cũ để so sánh.
/// Bảng: document.submission_file_versions
/// </summary>
public sealed class SubmissionFileVersion
{
    public const string Submission = "SUBMISSION", SupportingDocument = "SUPPORTING_DOCUMENT";

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ApplicationId { get; init; }
    public Guid? ChecklistId { get; init; }
    public string SourceType { get; init; } = Submission;
    public Guid SourceId { get; init; }
    public int ApplicationVersionNumber { get; init; }
    public string BlobUrl { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public string Sha256 { get; init; } = string.Empty;
    /// <summary>Dữ liệu ô điền của tờ khai trực tuyến (JSON) tại lần gửi; null với giấy tờ tải lên hoặc DOCX tải nguyên.</summary>
    public string? FormData { get; init; }
    /// <summary>Thời điểm tệp nguồn được tải lên/sửa lần cuối.</summary>
    public DateTime SourceUpdatedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
