namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class ChecklistItemSchema
{
    public string ChecklistId { get; set; } = string.Empty;
    public string? CaseCode { get; set; }
    public string SubmissionType { get; set; } = "NOP";
    public string ItemName { get; set; } = string.Empty;
    // Một loại bản duy nhất; null khi giấy tờ cần nhiều loại bản (xem CopyRequirements) hoặc không quy định.
    public string? DocumentCopyType { get; set; } = "ORIGINAL";
    // Tổng số bản; khi có CopyRequirements thì BE tính lại bằng tổng các dòng.
    public int Quantity { get; set; } = 1;
    // PROC-02a: nhiều loại bản cho cùng giấy tờ, ví dụ 1 bản chính + 1 bản sao.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<CopyRequirement>? CopyRequirements { get; set; }
    public string? ConditionNote { get; set; }
    public bool IsMandatory { get; set; } = true;
}
