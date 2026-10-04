using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Domain.Errors;

/// <summary>
/// Danh mục mã lỗi nghiệp vụ của Document & Form Service.
/// </summary>
public static class DocumentFormErrors
{
    public static Error TemplateNotFound(Guid templateId) =>
        Error.NotFound("document.template_not_found", $"Form template with ID '{templateId}' was not found.");

    public static Error TemplateCodeAlreadyExists(string code) =>
        Error.Conflict("document.template_code_conflict", $"Form template with code '{code}' already exists.");

    public static Error VersionNotFound(Guid templateId, int versionNumber) =>
        Error.NotFound("document.version_not_found", $"Version '{versionNumber}' for template '{templateId}' was not found.");

    public static Error InvalidSchema(string message) =>
        Error.Validation("document.invalid_schema", message);

    public static Error InvalidFormData(string message) =>
        Error.Validation("document.invalid_form_data", message);

    public static Error InvalidDocxFile(string message) =>
        Error.Validation("document.invalid_docx_file", message);

    public static Error EmptyFile =>
        Error.Validation("document.empty_file", "The uploaded file is empty.");

    public static Error FileTooLarge(long maxBytes) =>
        Error.Validation("document.file_too_large", $"File size exceeds the maximum allowed limit of {maxBytes} bytes.");

    public static Error PlaceholderMismatch(string details) =>
        Error.Validation("document.placeholder_mismatch", details);

    public static Error FileNotUploaded =>
        Error.NotFound("document.file_not_uploaded", "No DOCX file has been uploaded for this template yet.");
}
