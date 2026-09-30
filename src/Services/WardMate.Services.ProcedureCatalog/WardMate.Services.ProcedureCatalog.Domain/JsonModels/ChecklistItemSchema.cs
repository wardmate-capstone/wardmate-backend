namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class ChecklistItemSchema
{
    public string ChecklistId { get; set; } = string.Empty;
    public string? CaseCode { get; set; }
    public string SubmissionType { get; set; } = "NOP";
    public string ItemName { get; set; } = string.Empty;
    public string DocumentCopyType { get; set; } = "ORIGINAL";
    public int Quantity { get; set; } = 1;
    public string? ConditionNote { get; set; }
    public bool IsMandatory { get; set; } = true;
}
