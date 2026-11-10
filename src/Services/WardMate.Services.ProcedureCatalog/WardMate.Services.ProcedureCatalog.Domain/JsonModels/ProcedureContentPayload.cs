using System.Text.Json.Serialization;

namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

public sealed class ProcedureContentPayload
{
    public string DecisionNumber { get; set; } = string.Empty;
    public string ReceivingAddress { get; set; } = string.Empty;
    public List<SubmissionMethodDetail> SubmissionMethods { get; set; } = [];
    public List<LegalReferenceDetail> LegalReferences { get; set; } = [];
    public List<string> Results { get; set; } = [];
    public List<ProcedureCaseDetail> Cases { get; set; } = [];

    // BE-H (B.2) — thông tin chung theo khung Cổng DVC. Đều tuỳ chọn; null khi không có.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcedureType { get; set; }
    /// <summary>Nhiều cấp thực hiện; BE ghép vào levelOfImplementation nếu trường đó trống.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Levels { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CompetentAuthorities { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ExecutingAgencies { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AuthorizedAgencies { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CoordinatingAgencies { get; set; }
    /// <summary>Yêu cầu, điều kiện thực hiện (văn bản dài, nhiều dòng).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Requirements { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ReceivingAddresses { get; set; }
    /// <summary>Kết quả kèm mã; BE tự điền results = danh sách name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ResultDetail>? ResultDetails { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Keywords { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    // BE-H (B.4) — trình tự: nguyên văn, bước chung, hoặc bước theo trường hợp (cases[].steps).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StepsText { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProcedureStepDetail>? CommonSteps { get; set; }

    // BE-H (Phần C) — mục mở rộng tự do, hiển thị theo thứ tự sau các mục chuẩn.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AdditionalSection>? AdditionalSections { get; set; }
}
