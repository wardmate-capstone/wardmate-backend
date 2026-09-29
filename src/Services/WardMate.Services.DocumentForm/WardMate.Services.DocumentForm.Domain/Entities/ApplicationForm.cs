using WardMate.SharedKernel.Domain;

namespace WardMate.Services.DocumentForm.Domain.Entities;

/// <summary>
/// Đại diện cho dữ liệu biểu mẫu đã được điền của một hồ sơ hành chính (Application).
/// Bảng: document.application_forms
/// </summary>
public sealed class ApplicationForm : BaseEntity
{
    private ApplicationForm()
    {
    }

    public ApplicationForm(
        Guid applicationId,
        string formData,
        string? createdBy = null)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("Application ID cannot be empty.", nameof(applicationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(formData);

        ApplicationId = applicationId;
        FormData = formData;
        CreatedBy = createdBy;
        SetCreated();
    }

    public Guid ApplicationId { get; private set; }
    public string FormData { get; private set; } = string.Empty;
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public void UpdateFormData(string formData, string? updatedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formData);
        FormData = formData;
        UpdatedBy = updatedBy;
        SetUpdated();
    }
}
