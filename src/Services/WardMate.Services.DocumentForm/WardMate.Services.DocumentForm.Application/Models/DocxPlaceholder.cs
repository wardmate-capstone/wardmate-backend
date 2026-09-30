namespace WardMate.Services.DocumentForm.Application.Models;

/// <summary>
/// Đại diện cho một placeholder được bóc tách từ tệp Word DOCX.
/// Ví dụ: {{ho_va_ten}}, {{ngay_thang_nam_sinh}}, {{so_cccd}}
/// </summary>
public sealed record DocxPlaceholder
{
    public string Name { get; init; } = string.Empty;
    public string RawTag { get; init; } = string.Empty;
    public int Occurrences { get; init; } = 1;
    public IReadOnlyList<string> Locations { get; init; } = [];
}
