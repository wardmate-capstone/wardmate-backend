using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class AnalyticsService(AnalyticsDbContext db, IAnalyticsWorkflowClient workflow, TimeProvider clock) : IAnalyticsService
{
    private static readonly MetricTotals Zero = new(0, 0, 0, 0, 0, 0, 0);
    private static MetricTotals Totals(IEnumerable<ProcedureMetric> rows) => new(
        rows.Sum(x => (long)x.ReceivedCount), rows.Sum(x => (long)x.ReadyCount), rows.Sum(x => (long)x.RevisionRequestedCount),
        rows.Sum(x => (long)x.ResubmittedCount), rows.Sum(x => (long)x.CancelledCount),
        rows.Sum(x => (long)x.LegacyApprovedCount), rows.Sum(x => (long)x.LegacyRejectedCount));
    private IQueryable<ProcedureMetric> Metrics(string ward, AnalyticsQuery q) => db.Procedures.AsNoTracking()
        .Where(x => x.WardCode == ward && x.MetricDate >= q.From && x.MetricDate <= q.To &&
            (q.ProcedureId == null || x.ProcedureId == q.ProcedureId));
    private async Task<Coverage> Coverage(AnalyticsQuery q, CancellationToken ct)
    {
        var days = await db.Refreshes.AsNoTracking().Where(x => x.MetricDate >= q.From && x.MetricDate <= q.To).ToArrayAsync(ct);
        var dates = days.Select(x => x.MetricDate).ToHashSet();
        return new(q.From, q.To, Enumerable.Range(0, q.To.DayNumber - q.From.DayNumber + 1)
            .Select(q.From.AddDays).Where(x => !dates.Contains(x)).ToArray(),
            days.Length == 0 ? null : days.Min(x => x.SourceAtUtc), days.Length == 0 ? null : days.Max(x => x.RefreshedAtUtc));
    }
    // Coverage and metrics are read from the same database snapshot while jobs may replace a day.
    public async Task<DashboardOverview> Overview(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        var deadlines = DeadlineAggregation.Aggregate(await workflow.ReadDeadlines(ward, q, ct));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        return new(Totals(await Metrics(ward, q).ToArrayAsync(ct)), await Coverage(q, ct),
            deadlines.CompletedSampleCount == 0 ? null : 100.0 * deadlines.CompletedOnTime / deadlines.CompletedSampleCount,
            deadlines.CompletedSampleCount == 0 ? null : 100.0 * deadlines.CompletedLate / deadlines.CompletedSampleCount,
            deadlines.CompletedSampleCount == 0 ? "NO_KNOWN_COMPLETED_DEADLINES" : "AVAILABLE", deadlines);
    }
    private async Task<DeadlineFacts> DeadlineSource(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        var source = await workflow.ReadDeadlines(ward, q, ct);
        DeadlineAggregation.ValidateDimensions(source);
        return source;
    }
    public async Task<TrendResult> Trends(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        var source = await DeadlineSource(ward, q, ct);
        var completed = source.Items.Where(x => x.DocumentsReadyAtUtc.HasValue)
            .ToLookup(x => DateOnly.FromDateTime(x.DocumentsReadyAtUtc!.Value.AddHours(7)));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var groups = (await Metrics(ward, q).ToArrayAsync(ct)).GroupBy(x => x.MetricDate).ToDictionary(x => x.Key, Totals);
        return new(Enumerable.Range(0, q.To.DayNumber - q.From.DayNumber + 1).Select(q.From.AddDays)
            .Select(d => new TrendPoint(d, groups.GetValueOrDefault(d, Zero), DeadlineAggregation.Subset(source, completed[d]))).ToArray(),
            await Coverage(q, ct), DeadlineAggregation.Subset(source, source.Items.Where(x => x.DocumentsReadyAtUtc is null)));
    }
    public async Task<AnalyticsPage<ProcedureStatistics>> Procedures(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        var source = await DeadlineSource(ward, q, ct);
        var deadlines = source.Items.ToLookup(x => x.ProcedureId);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var metrics = (await Metrics(ward, q).ToArrayAsync(ct)).GroupBy(x => x.ProcedureId).ToDictionary(x => x.Key, Totals);
        // Include live-only groups even when the cached aggregation job has not run yet.
        var rows = metrics.Keys.Union(deadlines.Select(x => x.Key))
            .Select(id => new ProcedureStatistics(id, metrics.GetValueOrDefault(id, Zero), DeadlineAggregation.Subset(source, deadlines[id])))
            .OrderByDescending(x => x.Totals.ReceivedCount).ThenBy(x => x.ProcedureId).ToArray();
        return new(rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArray(), q.Page, q.PageSize, rows.Length, await Coverage(q, ct));
    }
    public async Task<AnalyticsPage<OfficerStatistics>> Officers(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        var source = await DeadlineSource(ward, q, ct);
        var deadlines = source.Items.Where(x => x.AssignedOfficerId.HasValue).ToLookup(x => x.AssignedOfficerId!.Value);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var metrics = (await db.Officers.AsNoTracking().Where(x => x.WardCode == ward && x.MetricDate >= q.From && x.MetricDate <= q.To &&
            (q.ProcedureId == null || x.ProcedureId == q.ProcedureId)).ToArrayAsync(ct)).ToLookup(x => x.OfficerId);
        var rows = metrics.Select(x => x.Key).Union(deadlines.Select(x => x.Key)).Select(id =>
        {
            var g = metrics[id].ToArray();
            var samples = g.Sum(x => (long)x.DurationSampleCount);
            return new OfficerStatistics(id, g.Sum(x => (long)x.ReadyCount), g.Sum(x => (long)x.RevisionRequestedCount),
                samples, samples == 0 ? null : g.Sum(x => x.TotalProcessingMinutes) / samples,
                DeadlineAggregation.Subset(source, deadlines[id]));
        }).OrderByDescending(x => x.ReadyCount).ThenBy(x => x.OfficerId).ToArray();
        return new(rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArray(), q.Page, q.PageSize, rows.Length, await Coverage(q, ct),
            DeadlineAggregation.Subset(source, source.Items.Where(x => x.AssignedOfficerId is null)));
    }
    public async Task<FeedbackDto> CreateFeedback(Guid citizen, Guid id, string token, CreateFeedback input, CancellationToken ct)
    {
        var application = await workflow.ReadOwned(id, token, ct);
        if (application.Id != id || application.UserId != citizen)
            throw new AnalyticsException(404, "feedback.application_not_found", "Không tìm thấy hồ sơ trong phạm vi của bạn.");
        if (application.Status is not ("DOCUMENTS_READY" or "OFFICIALLY_RECEIVED"))
            throw new AnalyticsException(409, "feedback.not_ready", "Chỉ đánh giá hồ sơ DOCUMENTS_READY hoặc OFFICIALLY_RECEIVED.");
        if (application.ProcedureId == Guid.Empty || string.IsNullOrWhiteSpace(application.WardCode) || application.WardCode.Length > 50)
            throw new AnalyticsException(503, "feedback.invalid_source", "Hồ sơ thiếu thông tin thủ tục/phường hợp lệ.");
        var row = new CitizenFeedback { ApplicationId = id, CitizenId = citizen, ProcedureId = application.ProcedureId,
            WardCode = application.WardCode, Rating = input.Rating, Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        db.Feedback.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new AnalyticsException(409, "feedback.already_exists", "Hồ sơ đã được đánh giá. Hãy tải lại đánh giá hiện có."); }
        return Dto(row);
    }
    public async Task<FeedbackDto> GetFeedback(Guid citizen, Guid id, CancellationToken ct) =>
        await db.Feedback.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicationId == id && x.CitizenId == citizen, ct) is { } row
            ? Dto(row) : throw new AnalyticsException(404, "feedback.not_found", "Chưa có đánh giá của bạn cho hồ sơ này.");
    private IQueryable<CitizenFeedback> FeedbackRows(string ward, AnalyticsQuery q)
    {
        var start = new DateTimeOffset(q.From.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).UtcDateTime;
        var end = new DateTimeOffset(q.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).UtcDateTime;
        return db.Feedback.AsNoTracking().Where(x => x.WardCode == ward && x.CreatedAtUtc >= start && x.CreatedAtUtc < end &&
            (q.ProcedureId == null || x.ProcedureId == q.ProcedureId));
    }
    public async Task<FeedbackSummary> FeedbackSummary(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        var groups = await FeedbackRows(ward, q).GroupBy(x => x.Rating).Select(g => new { Rating = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var counts = Enumerable.Range(1, 5).ToDictionary(x => x, x => groups.SingleOrDefault(g => g.Rating == x)?.Count ?? 0);
        var total = counts.Values.Sum();
        return new(total, total == 0 ? null : counts.Sum(x => (double)x.Key * x.Value) / total,
            total == 0 ? null : 100.0 * (counts[4] + counts[5]) / total, counts);
    }
    public async Task<AnalyticsPage<FeedbackDto>> FeedbackList(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var rows = FeedbackRows(ward, q);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        return new(items.Select(Dto).ToArray(), q.Page, q.PageSize, count);
    }
    private static FeedbackDto Dto(CitizenFeedback f) => new(f.Id, f.ApplicationId, f.Rating, f.Comment, f.CreatedAtUtc);
}
