namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class LegalReferenceDetail
{
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public DateOnly? IssueDate { get; set; }
    public string Authority { get; set; } = string.Empty;
}
