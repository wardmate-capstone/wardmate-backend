namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class SubmissionMethodDetail
{
    public string MethodName { get; set; } = string.Empty;
    public decimal FeeAmount { get; set; }
    public string FeeUnit { get; set; } = "VND";
    public decimal EstimatedDays { get; set; }
    public string? Note { get; set; }
}
