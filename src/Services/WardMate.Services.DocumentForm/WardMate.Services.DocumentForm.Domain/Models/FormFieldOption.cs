namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>
/// Lựa chọn cho các trường dạng Select, Radio, Checkbox trên biểu mẫu.
/// </summary>
public sealed record FormFieldOption
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
