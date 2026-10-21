using System.ComponentModel.DataAnnotations;

namespace WardMate.Services.AnalyticsSystem.Application;

public sealed record ProcedureDay(Guid ProcedureId, string WardCode, int ReceivedCount, int ReadyCount,
    int RevisionRequestedCount, int ResubmittedCount, int CancelledCount, int LegacyApprovedCount, int LegacyRejectedCount);
public sealed record OfficerDay(Guid OfficerId, Guid ProcedureId, string WardCode, int ReadyCount,
    int RevisionRequestedCount, int DurationSampleCount, double TotalProcessingMinutes);
public sealed record AnalyticsDay(int ContractVersion, DateOnly Date, DateTime GeneratedAtUtc, string TimeZone,
    ProcedureDay[] Procedures, OfficerDay[] Officers);
public sealed record FeedbackApplication(Guid Id, Guid UserId, Guid ProcedureId, string Status, string? WardCode);
public interface IAnalyticsWorkflowClient
{
    Task<DeadlineFacts> ReadDeadlines(string ward, AnalyticsQuery query, CancellationToken ct);
    Task<AnalyticsDay> ReadDay(DateOnly date, CancellationToken ct);
    Task<FeedbackApplication> ReadOwned(Guid id, string bearerToken, CancellationToken ct);
}
public sealed class AnalyticsException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed class AnalyticsQuery
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public Guid? ProcedureId { get; set; }
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public sealed record MetricTotals(long ReceivedCount, long ReadyCount, long RevisionRequestedCount,
    long ResubmittedCount, long CancelledCount, long LegacyApprovedCount, long LegacyRejectedCount);
public sealed record Coverage(DateOnly From, DateOnly To, DateOnly[] MissingDates, DateTime? OldestSourceAtUtc,
    DateTime? LatestRefreshAtUtc, string TimeZone = "Asia/Ho_Chi_Minh");
public sealed record DashboardOverview(MetricTotals Totals, Coverage Coverage, double? OnTimeRate,
    double? LateRate, string TimelinessStatus = "UNAVAILABLE_NO_WORKFLOW_DEADLINE", DeadlineStatistics? Deadlines = null);
public sealed record TrendPoint(DateOnly Date, MetricTotals Totals);
public sealed record TrendResult(TrendPoint[] Items, Coverage Coverage);
public sealed record ProcedureStatistics(Guid ProcedureId, MetricTotals Totals);
public sealed record OfficerStatistics(Guid OfficerId, long ReadyCount, long RevisionRequestedCount,
    long DurationSampleCount, double? AverageElapsedMinutes);
public sealed record AnalyticsPage<T>(T[] Items, int Page, int PageSize, int TotalCount, Coverage? Coverage = null);
public sealed record CreateFeedback([Range(1, 5)] int Rating,
    [StringLength(2000)] string? Comment);
public sealed record FeedbackDto(Guid Id, Guid ApplicationId, int Rating, string? Comment, DateTime CreatedAtUtc);
public sealed record FeedbackSummary(int TotalCount, double? AverageRating, double? SatisfactionRate,
    Dictionary<int, int> RatingCounts);
public interface IAnalyticsService
{
    Task<DashboardOverview> Overview(string ward, AnalyticsQuery query, CancellationToken ct);
    Task<TrendResult> Trends(string ward, AnalyticsQuery query, CancellationToken ct);
    Task<AnalyticsPage<ProcedureStatistics>> Procedures(string ward, AnalyticsQuery query, CancellationToken ct);
    Task<AnalyticsPage<OfficerStatistics>> Officers(string ward, AnalyticsQuery query, CancellationToken ct);
    Task<FeedbackDto> CreateFeedback(Guid citizen, Guid applicationId, string token, CreateFeedback input, CancellationToken ct);
    Task<FeedbackDto> GetFeedback(Guid citizen, Guid applicationId, CancellationToken ct);
    Task<FeedbackSummary> FeedbackSummary(string ward, AnalyticsQuery query, CancellationToken ct);
    Task<AnalyticsPage<FeedbackDto>> FeedbackList(string ward, AnalyticsQuery query, CancellationToken ct);
}
