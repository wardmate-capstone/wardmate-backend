namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>
/// Định nghĩa cấu hình một trường nhập liệu (Field) trong biểu mẫu điện tử.
/// </summary>
public sealed record FormField
{
    public string FieldId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public FormFieldType Type { get; init; } = FormFieldType.Text;
    public string? Placeholder { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsRequired { get; init; }
    public int Order { get; init; }
    public string? HelpText { get; init; }
    public FormFieldValidation? Validation { get; init; }
    public IReadOnlyList<FormFieldOption> Options { get; init; } = [];
}
