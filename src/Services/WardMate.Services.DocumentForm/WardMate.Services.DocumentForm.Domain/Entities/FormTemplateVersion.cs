using WardMate.SharedKernel.Domain;

namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// Đại diện cho một phiên bản cấu hình schema động của biểu mẫu điện tử.
/// Bảng: document.form_template_versions
/// </summary>
public sealed class FormTemplateVersion : BaseEntity
{
    private FormTemplateVersion()
    {
    }

    public FormTemplateVersion(
        Guid templateId,
        int versionNumber,
        string schemaDefinition,
        string? createdBy = null)
    {
        if (templateId == Guid.Empty)
        {
            throw new ArgumentException("Template ID cannot be empty.", nameof(templateId));
        }

        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(schemaDefinition);

        TemplateId = templateId;
        VersionNumber = versionNumber;
        SchemaDefinition = schemaDefinition;
        CreatedBy = createdBy;
        SetCreated();
    }

    public Guid TemplateId { get; private set; }
    public int VersionNumber { get; private set; }
    public string SchemaDefinition { get; private set; } = string.Empty;
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public FormTemplate? Template { get; private set; }

    public void UpdateSchemaDefinition(string schemaDefinition, string? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaDefinition);
        SchemaDefinition = schemaDefinition;
        UpdatedBy = updatedBy;
        SetUpdated();
    }
}
