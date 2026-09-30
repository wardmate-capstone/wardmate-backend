namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class FormDefinitionSchema
{
    // External identifier only: no cross-service database FK or navigation.
    public Guid? FormTemplateId { get; set; }
    public string? CaseCode { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public string FormType { get; set; } = "ONLINE_INTERACTIVE";
    public int Quantity { get; set; } = 1;
    public bool IsMandatory { get; set; } = true;
}
