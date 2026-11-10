namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

/// <summary>
/// BE-H (B.1): một mức phí/lệ phí trong một cách nộp. Có thể là số tiền, tỷ lệ phần trăm,
/// hoặc chỉ có mô tả ("theo quy định...") khi không tách được thành số.
/// </summary>
public sealed class FeeItem
{
    /// <summary>"PHI" | "LE_PHI" | null.</summary>
    public string? Kind { get; set; }
    /// <summary>Số tiền; null khi là tỷ lệ hoặc "theo quy định".</summary>
    public decimal? Amount { get; set; }
    /// <summary>Tỷ lệ phần trăm, ví dụ 0.5 (%).</summary>
    public decimal? Percent { get; set; }
    public string? Unit { get; set; } = "VND";
    /// <summary>Áp dụng cho ai / trường hợp nào.</summary>
    public string? Description { get; set; }
    public string? CaseCode { get; set; }
}
