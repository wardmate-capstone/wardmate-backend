using System.Text.Json.Serialization;

namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class SubmissionMethodDetail
{
    public string MethodName { get; set; } = string.Empty;
    // BE-H: null = không rõ / theo văn bản. Có Fees thì BE tự gán bằng mức nhỏ nhất có Amount.
    public decimal? FeeAmount { get; set; }
    public string FeeUnit { get; set; } = "VND";
    // BE-H: null = không quy định / tính theo giờ. ProcessingTime đơn vị ngày thì BE tự gán bằng Value.
    public decimal? EstimatedDays { get; set; }
    public string? Note { get; set; }
    // BE-H (B.1): cách nộp chỉ áp dụng cho một trường hợp.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CaseCode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FeeItem>? Fees { get; set; }
    /// <summary>Nguyên văn phí/lệ phí khi không tách được thành số.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FeeText { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProcessingTime? ProcessingTime { get; set; }
}
