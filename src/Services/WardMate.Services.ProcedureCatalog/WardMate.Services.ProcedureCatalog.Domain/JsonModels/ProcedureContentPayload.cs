namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class ProcedureContentPayload
{
    public string DecisionNumber { get; set; } = string.Empty;
    public string ReceivingAddress { get; set; } = string.Empty;
    public List<SubmissionMethodDetail> SubmissionMethods { get; set; } = [];
    public List<LegalReferenceDetail> LegalReferences { get; set; } = [];
    public List<string> Results { get; set; } = [];
    public List<ProcedureCaseDetail> Cases { get; set; } = [];
}
