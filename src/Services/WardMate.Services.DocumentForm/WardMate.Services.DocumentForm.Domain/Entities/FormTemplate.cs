using WardMate.SharedKernel.Domain;

namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// Đại diện cho biểu mẫu điện tử phôi (Form Template) trong hệ thống tài liệu.
/// Bảng: document.form_templates
/// </summary>
public sealed class FormTemplate : BaseEntity
{
    private readonly List<FormTemplateVersion> _versions = [];

    private FormTemplate()
    {
    }

    public FormTemplate(
        string code,
        string title,
        string? fileDocxUrl = null,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Code = code.Trim().ToUpperInvariant();
        Title = title.Trim();
        FileDocxUrl = string.IsNullOrWhiteSpace(fileDocxUrl) ? null : fileDocxUrl.Trim();
        IsActive = true;
        CreatedBy = createdBy;
        SetCreated(DateTime.UtcNow);
    }

    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? FileDocxUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public IReadOnlyCollection<FormTemplateVersion> Versions => _versions.AsReadOnly();

    public void UpdateDetails(string title, string? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }

    public void UpdateDocxUrl(string docxUrl, string? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docxUrl);
        FileDocxUrl = docxUrl.Trim();
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }

    public void Activate(string? updatedBy = null)
    {
        IsActive = true;
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }

    public void Deactivate(string? updatedBy = null)
    {
        IsActive = false;
        UpdatedBy = updatedBy;
        SetUpdated(DateTime.UtcNow);
    }

    public FormTemplateVersion AddVersion(string schemaDefinition, string? createdBy = null)
    {
        var nextVersionNumber = _versions.Count == 0
            ? 1
            : _versions.Max(v => v.VersionNumber) + 1;

        var version = new FormTemplateVersion(Id, nextVersionNumber, schemaDefinition, createdBy);
        _versions.Add(version);
        SetUpdated(DateTime.UtcNow);
        return version;
    }
}
