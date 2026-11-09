namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

/// <summary>
/// Một loại bản và số lượng của một giấy tờ, ví dụ "01 bản chính" + "01 bản sao" (PROC-02a).
/// </summary>
public sealed class CopyRequirement
{
    public string DocumentCopyType { get; set; } = "ORIGINAL";
    public int Quantity { get; set; } = 1;
}
