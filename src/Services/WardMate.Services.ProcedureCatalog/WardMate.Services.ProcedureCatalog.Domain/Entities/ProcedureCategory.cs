namespace WardMate.Services.ProcedureCatalog.Domain.Entities;

public sealed class ProcedureCategory
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Procedure> Procedures { get; set; } = new List<Procedure>();
}
