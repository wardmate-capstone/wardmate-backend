namespace WardMate.Services.DocumentForm.Application.Models;

/// <summary>
/// Chi tiết lỗi xác thực dữ liệu trên từng ô nhập liệu của biểu mẫu.
/// </summary>
public sealed record FormDataValidationError
{
    public string FieldId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
}
