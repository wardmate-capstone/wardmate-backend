using WardMate.Services.DocumentForm.Domain.Models;

namespace WardMate.Services.DocumentForm.Application.DTOs;

/// <summary>
/// Thông tin tóm tắt của một hồ sơ người dùng (dùng cho danh sách).
/// </summary>
public sealed record UserSubmissionSummaryDto
{
    public Guid? TemplateVersionId { get; init; }
    public System.Text.Json.JsonElement? FormData { get; init; }
    public System.Text.Json.JsonElement? SchemaDefinition { get; init; }
    public Guid Id { get; init; }
    public Guid TemplateId { get; init; }
    public Guid ApplicantId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? OfficerComment { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Kết quả trả về sau khi lưu nháp hoặc nộp hồ sơ.
/// </summary>
public sealed record SaveSubmissionResultDto
{
    public Guid? TemplateVersionId { get; init; }
    public Guid SubmissionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
