using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Domain.Entities;

public sealed class Procedure
{
    public Guid Id { get; set; }
    public int CategoryId { get; set; }
    public string ProcedureCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? OriginalPdfUrl { get; set; }
    public string? PdfFileName { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? ExecutingAgency { get; set; }
    public string? LevelOfImplementation { get; set; }
    public string? TargetAudience { get; set; }
    public string? FeeSummary { get; set; }
    public string? ProcessingTimeSummary { get; set; }
    /// <summary>BE-H: contentPayload.keywords ghép thành chuỗi để tìm kiếm (keyword) — BE tự tính, client không gửi.</summary>
    public string? SearchKeywords { get; set; }
    /// <summary>PROC-UNPUBLISH-01: lần đầu IsActive thành true; còn null thì người dân chưa từng thấy nên được hủy xuất bản.</summary>
    public DateTime? FirstPublishedAt { get; set; }
    public bool CanUnpublish => !IsActive && FirstPublishedAt is null;
    public bool IsActive { get; set; } = true;
    public string? StatusChangeReason { get; set; }
    public ProcedureContentPayload ContentPayload { get; set; } = new();
    public List<ChecklistItemSchema>? ChecklistSchema { get; set; }
    public List<FormDefinitionSchema>? FormDefinitions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ProcedureCategory Category { get; set; } = null!;
    public ICollection<ProcedureVersion> Versions { get; set; } = new List<ProcedureVersion>();
}
