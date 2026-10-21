namespace WardMate.Services.AnalyticsSystem.Domain;

public sealed class ProcedureMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid ProcedureId { get; set; }
    public string WardCode { get; set; } = "";
    public int ReceivedCount { get; set; }
    public int ReadyCount { get; set; }
    public int RevisionRequestedCount { get; set; }
    public int ResubmittedCount { get; set; }
    public int CancelledCount { get; set; }
    public int LegacyApprovedCount { get; set; }
    public int LegacyRejectedCount { get; set; }
}

public sealed class OfficerMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid OfficerId { get; set; }
    public Guid ProcedureId { get; set; }
    public string WardCode { get; set; } = "";
    public int ReadyCount { get; set; }
    public int RevisionRequestedCount { get; set; }
    public int DurationSampleCount { get; set; }
    public double TotalProcessingMinutes { get; set; }
}

public sealed class MetricRefresh
{
    public DateOnly MetricDate { get; set; }
    public DateTime SourceAtUtc { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}

public sealed class CitizenFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid CitizenId { get; set; }
    public Guid ProcedureId { get; set; }
    public string WardCode { get; set; } = "";
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
