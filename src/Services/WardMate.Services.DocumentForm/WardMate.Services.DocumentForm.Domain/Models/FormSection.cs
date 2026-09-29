namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>
/// Định nghĩa một phân mục (Section/Nhóm trường) trên biểu mẫu động.
/// </summary>
public sealed record FormSection
{
    public string SectionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Order { get; init; }
    public IReadOnlyList<FormField> Fields { get; init; } = [];
}
