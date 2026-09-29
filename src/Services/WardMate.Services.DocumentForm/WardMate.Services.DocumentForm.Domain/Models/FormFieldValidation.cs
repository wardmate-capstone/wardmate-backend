namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>
/// Các quy tắc kiểm tra tính hợp lệ (Validation Rules) cấu hình trên từng ô nhập liệu của biểu mẫu.
/// </summary>
public sealed record FormFieldValidation
{
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
    public string? RegexPattern { get; init; }
    public string? CustomErrorMessage { get; init; }
}
