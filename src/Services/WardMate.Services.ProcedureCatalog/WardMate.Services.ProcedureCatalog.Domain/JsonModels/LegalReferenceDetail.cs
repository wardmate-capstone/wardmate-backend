namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

/// <summary>
/// PROC-LEGAL-01: một dòng "Căn cứ pháp lý" theo bảng 4 cột của Cổng DVC (Số ký hiệu, Trích yếu, Ngày ban hành, Cơ quan ban hành).
/// Mỗi cột được để trống (null) khi nguồn không ghi; dòng phải có ít nhất số ký hiệu hoặc trích yếu.
/// Bảng 2 cột (Tên văn bản, Mã văn bản) = DocumentName + DocumentNumber, hai cột còn lại null.
/// </summary>
public sealed class LegalReferenceDetail
{
    /// <summary>Số ký hiệu / mã văn bản, ví dụ "168/2025/NĐ-CP".</summary>
    public string? DocumentNumber { get; set; } = string.Empty;
    /// <summary>Trích yếu / tên văn bản.</summary>
    public string? DocumentName { get; set; } = string.Empty;
    public DateOnly? IssueDate { get; set; }
    /// <summary>Cơ quan ban hành.</summary>
    public string? Authority { get; set; } = string.Empty;
}
