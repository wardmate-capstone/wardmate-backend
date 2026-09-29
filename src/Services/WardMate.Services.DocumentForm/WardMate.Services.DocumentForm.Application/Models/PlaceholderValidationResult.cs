namespace WardMate.Services.DocumentForm.Application.Models;

/// <summary>
/// Kết quả so khớp giữa danh sách placeholders trong file DOCX và các trường cấu hình trong FormSchemaDefinition.
/// </summary>
public sealed record PlaceholderValidationResult
{
    public IReadOnlyList<string> MatchedFields { get; init; } = [];
    public IReadOnlyList<string> MissingInSchema { get; init; } = [];
    public IReadOnlyList<string> UnusedInDocx { get; init; } = [];

    public bool IsValid => MissingInSchema.Count == 0;
}
