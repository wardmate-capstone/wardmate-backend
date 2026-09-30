namespace WardMate.Services.DocumentForm.Application.DTOs;

public sealed record FormTemplateDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? FileDocxUrl { get; init; }
    public bool IsActive { get; init; }
    public int LatestVersion { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed record FormTemplateVersionDto
{
    public Guid Id { get; init; }
    public Guid TemplateId { get; init; }
    public int VersionNumber { get; init; }
    public string SchemaDefinition { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed record FormTemplateDetailDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? FileDocxUrl { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public IReadOnlyList<FormTemplateVersionDto> Versions { get; init; } = [];
}
