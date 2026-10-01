using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public class ProcedureInput
{
    public int CategoryId { get; set; }
    public string ProcedureCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? IssuingAuthority { get; set; }
    public string? ExecutingAgency { get; set; }
    public string LevelOfImplementation { get; set; } = "Cấp Xã";
    public string TargetAudience { get; set; } = "Công dân Việt Nam";
    public string FeeSummary { get; set; } = "Miễn phí";
    public string ProcessingTimeSummary { get; set; } = "1 ngày";
    public ProcedureContentPayload ContentPayload { get; set; } = new();
    public List<ChecklistItemSchema>? ChecklistSchema { get; set; }
    public List<FormDefinitionSchema>? FormDefinitions { get; set; }

    public void ApplyTo(Procedure procedure)
    {
        procedure.CategoryId = CategoryId;
        procedure.ProcedureCode = ProcedureCode.Trim();
        procedure.Title = Title.Trim();
        procedure.IssuingAuthority = IssuingAuthority;
        procedure.ExecutingAgency = ExecutingAgency;
        procedure.LevelOfImplementation = LevelOfImplementation;
        procedure.TargetAudience = TargetAudience;
        procedure.FeeSummary = FeeSummary;
        procedure.ProcessingTimeSummary = ProcessingTimeSummary;
        procedure.ContentPayload = ContentPayload;
        procedure.ChecklistSchema = ChecklistSchema;
        procedure.FormDefinitions = FormDefinitions;
    }
}

public sealed class UpdateProcedureInput : ProcedureInput
{
    public string DecisionNumber { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
}
