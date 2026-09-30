namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>
/// Định nghĩa cấu trúc Schema toàn vẹn của một biểu mẫu điện tử (E-Form Schema Definition).
/// Được lưu trữ dưới dạng JSONB trong bảng document.form_template_versions.
/// </summary>
public sealed record FormSchemaDefinition
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Version { get; init; } = 1;
    public IReadOnlyList<FormSection> Sections { get; init; } = [];

    /// <summary>
    /// Lấy danh sách phẳng tất cả các trường (Fields) từ tất cả các phân mục (Sections).
    /// </summary>
    public IEnumerable<FormField> GetAllFields() => Sections.SelectMany(s => s.Fields);
}
