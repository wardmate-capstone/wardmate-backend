namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

/// <summary>BE-H (B.2): kết quả giải quyết kèm mã kết quả (ví dụ "KQ.H17.000312") và thời hạn hiệu lực.</summary>
public sealed class ResultDetail
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Validity { get; set; }
    public string? Note { get; set; }
}
