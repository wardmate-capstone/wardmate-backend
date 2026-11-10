namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class ChecklistItemSchema
{
    public string ChecklistId { get; set; } = string.Empty;
    public string? CaseCode { get; set; }
    public string SubmissionType { get; set; } = "NOP";
    public string ItemName { get; set; } = string.Empty;
    private string? documentCopyType = "ORIGINAL";
    private int quantity = 1;
    // Chỉ true khi setter được gọi (JSON gửi lên có trường này); không lưu xuống DB vì không phải property.
    private bool documentCopyTypeAssigned;
    private bool quantityAssigned;

    // Một loại bản duy nhất; null khi giấy tờ cần nhiều loại bản (xem CopyRequirements) hoặc không quy định.
    public string? DocumentCopyType
    {
        get => documentCopyType;
        set { documentCopyType = value; documentCopyTypeAssigned = true; }
    }
    // Tổng số bản; khi có CopyRequirements thì BE tính lại bằng tổng các dòng.
    public int Quantity
    {
        get => quantity;
        set { quantity = value; quantityAssigned = true; }
    }
    // PROC-02a: nhiều loại bản cho cùng giấy tờ, ví dụ 1 bản chính + 1 bản sao.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<CopyRequirement>? CopyRequirements { get; set; }
    public string? ConditionNote { get; set; }
    public bool IsMandatory { get; set; } = true;

    /// <summary>
    /// PROC-02b: dữ liệu gửi lên có khai rõ documentCopyType/quantity mâu thuẫn với CopyRequirements hay không.
    /// Bỏ trống hai trường cũ (hoặc gửi documentCopyType null) thì không tính là mâu thuẫn; gửi lại đúng giá trị từ GET cũng không.
    /// Chỉ có nghĩa trước khi BE chuẩn hóa (ProcedureInput.NormalizedChecklist).
    /// </summary>
    public bool ConflictsWithCopyRequirements()
    {
        if (CopyRequirements is not { Count: > 0 } copies || copies.Any(c => c is null)) return false;
        var impliedType = copies.Count == 1 ? copies[0].DocumentCopyType : null;
        var impliedQuantity = copies.Sum(c => (long)c.Quantity);
        return documentCopyTypeAssigned && documentCopyType is not null && documentCopyType != impliedType
            || quantityAssigned && quantity != impliedQuantity;
    }
}
