namespace WardMate.Services.DocumentForm.Application.DTOs;

public sealed record FormTemplateDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? FileDocxUrl { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed record FormTemplateDetailDto
{
    public Guid? TemplateVersionId { get; init; }
    public int? VersionNumber { get; init; }
    public System.Text.Json.JsonElement? SchemaDefinition { get; init; }
    public bool OnlineReady { get; init; }
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? FileDocxUrl { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
