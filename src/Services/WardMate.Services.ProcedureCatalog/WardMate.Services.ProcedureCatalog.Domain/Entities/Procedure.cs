using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Domain.Entities;

public sealed class Procedure
{
    public Guid Id { get; set; }
    public int CategoryId { get; set; }
    public string ProcedureCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? IssuingAuthority { get; set; }
    public string? ExecutingAgency { get; set; }
    public string LevelOfImplementation { get; set; } = "Cấp Xã";
    public string TargetAudience { get; set; } = "Công dân Việt Nam";
    public string FeeSummary { get; set; } = "Miễn phí";
    public string ProcessingTimeSummary { get; set; } = "1 ngày";
    public bool IsActive { get; set; } = true;
    public ProcedureContentPayload ContentPayload { get; set; } = new();
    public List<ChecklistItemSchema>? ChecklistSchema { get; set; }
    public List<FormDefinitionSchema>? FormDefinitions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ProcedureCategory Category { get; set; } = null!;
    public ICollection<ProcedureVersion> Versions { get; set; } = new List<ProcedureVersion>();
}
