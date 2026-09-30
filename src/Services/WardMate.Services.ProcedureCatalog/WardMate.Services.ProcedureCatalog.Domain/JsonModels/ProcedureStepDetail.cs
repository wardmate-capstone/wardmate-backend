namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class ProcedureStepDetail
{
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Executor { get; set; } = string.Empty;
    public string ActionDetails { get; set; } = string.Empty;
}
