namespace WardMate.Services.ProcedureCatalog.Domain.JsonModels;

/// <summary>
/// BE-H (B.1): thời hạn giải quyết của một cách nộp. Có số + đơn vị khi tách được,
/// và/hoặc nguyên văn ("Trong ngày", "Không quá 15 phút") khi không tách được.
/// </summary>
public sealed class ProcessingTime
{
    public decimal? Value { get; set; }
    /// <summary>"WORKING_DAY" | "DAY" | "HOUR" | "MINUTE" | null.</summary>
    public string? Unit { get; set; }
    public string? Text { get; set; }
}
