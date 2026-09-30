namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class ProcedureCaseDetail
{
    public string CaseCode { get; set; } = string.Empty;
    public string CaseName { get; set; } = string.Empty;
    public List<ProcedureStepDetail> Steps { get; set; } = [];
}
