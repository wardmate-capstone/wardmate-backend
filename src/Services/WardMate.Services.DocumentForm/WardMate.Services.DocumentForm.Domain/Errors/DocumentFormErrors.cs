using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Domain.Errors;

/// <summary>
/// Danh mục mã lỗi nghiệp vụ của Document & Form Service.
/// </summary>
public static class DocumentFormErrors
{
    public static Error TemplateNotFound(Guid templateId) =>
        Error.NotFound("document.template_not_found", $"Không tìm thấy biểu mẫu '{templateId}'.");

    public static Error TemplateCodeAlreadyExists(string code) =>
        Error.Conflict("document.template_code_conflict", $"Mã biểu mẫu '{code}' đã tồn tại.");

    public static Error VersionNotFound(Guid templateId, int versionNumber) =>
        Error.NotFound("document.version_not_found", $"Không tìm thấy phiên bản {versionNumber} của biểu mẫu '{templateId}'.");

    public static Error InvalidSchema(string message) =>
        Error.Validation("document.invalid_schema", message);

    public static Error InvalidFormData(string message) =>
        Error.Validation("document.invalid_form_data", message);

    public static Error InvalidDocxFile(string message) =>
        Error.Validation("document.invalid_docx_file", message);

    public static Error EmptyFile =>
        Error.Validation("document.empty_file", "Tệp tải lên đang trống.");

    public static Error FileTooLarge(long maxBytes) =>
        Error.Validation("document.file_too_large", $"Tệp vượt quá dung lượng cho phép ({FormatSize(maxBytes)}).");

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 && bytes % (1024 * 1024) == 0 ? $"{bytes / (1024 * 1024)} MB"
        : bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024):0.#} MB"
        : $"{bytes} byte";

    public static Error PlaceholderMismatch(string details) =>
        Error.Validation("document.placeholder_mismatch", details);

    public static Error FileNotUploaded =>
        Error.NotFound("document.file_not_uploaded", "Biểu mẫu này chưa có tệp Word (DOCX).");

    // ── UserSubmission errors ────────────────────────────────────────

    public static Error SubmissionNotFound(Guid submissionId) =>
        Error.NotFound("document.submission_not_found", $"Không tìm thấy bản khai '{submissionId}'.");

    public static Error SubmissionNotOwnedByApplicant(Guid submissionId, Guid applicantId) =>
        Error.Forbidden("document.submission_not_owned", "Bản khai này không thuộc tài khoản của bạn.");

    public static Error SubmissionInvalidStatusTransition(string currentStatus, string action) =>
        Error.Validation("document.submission_invalid_transition",
            $"Không thể thực hiện '{action}' khi bản khai đang ở trạng thái '{currentStatus}'.");

    public static Error SubmissionFileNotFound(Guid submissionId) =>
        Error.NotFound("document.submission_file_not_found",
            "Bản khai này chưa có tệp Word (DOCX).");

    public static Error BlobUploadFailed(string detail) =>
        Error.Failure("document.blob_upload_failed",
            "Không lưu được tệp lên hệ thống lưu trữ. Vui lòng thử lại sau.");

    public static Error FormDataRequired =>
        Error.Validation("document.form_data_required",
            "Cần nhập ít nhất một trường trong biểu mẫu.");

    public static Error SupportingDocumentNotFound(Guid documentId) =>
        Error.NotFound("document.supporting_document_not_found",
            "Không tìm thấy giấy tờ này trong hồ sơ.");

    /// <summary>DOC-FILE-MISSING-01: bản ghi còn nhưng tệp không còn trong kho lưu trữ (vd. mất blob khi chuyển hạ tầng).</summary>
    public static Error StoredFileMissing(Guid documentId) =>
        Error.NotFound("document.file_missing",
            "Tệp không còn trên hệ thống lưu trữ. Vui lòng tải lên lại (giấy tờ) hoặc tạo lại (bản PDF).");

    public static Error GeneratedDocumentNotFound(Guid documentId) =>
        Error.NotFound("document.generated_document_not_found",
            "Không tìm thấy bản PDF này của hồ sơ.");

    public static Error UnsupportedFileType(string details) =>
        Error.Validation("document.unsupported_file_type", details);

    public static Error PdfRenderFailed(string detail) =>
        Error.Failure("document.pdf_render_failed",
            $"Không thể xuất PDF: {detail}");

    public static Error SubmissionNotReady(Guid submissionId, string currentStatus) =>
        Error.Validation("document.submission_not_ready",
            $"Bản khai chưa được xác nhận giấy tờ sẵn sàng (trạng thái hiện tại: {currentStatus}). Chỉ xuất được bản PDF sau khi cán bộ kiểm tra và xác nhận giấy tờ sẵn sàng.");

    public static Error QrNotFound(Guid applicationId) =>
        Error.NotFound("document.qr_not_found",
            $"Mã QR tra cứu cho hồ sơ '{applicationId}' chưa được tạo hoặc hồ sơ chưa được xác nhận sẵn sàng.");
}

