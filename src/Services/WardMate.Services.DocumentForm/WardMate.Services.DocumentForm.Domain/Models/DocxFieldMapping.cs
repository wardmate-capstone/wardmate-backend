namespace WardMate.Services.DocumentForm.Domain.Models;

/// <summary>UTF-16 offsets in the paragraph text returned by docx-structure.</summary>
public sealed record DocxFieldMapping
{
    public string FieldId { get; init; } = string.Empty;
    public int ParagraphIndex { get; init; }
    public int Start { get; init; }
    public int Length { get; init; }
    public string ExpectedText { get; init; } = string.Empty;
}
