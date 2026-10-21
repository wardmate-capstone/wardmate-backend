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
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        return new(Totals(await Metrics(ward, q).ToArrayAsync(ct)), await Coverage(q, ct), null, null);
    }
    public async Task<TrendResult> Trends(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var groups = (await Metrics(ward, q).ToArrayAsync(ct)).GroupBy(x => x.MetricDate).ToDictionary(x => x.Key, Totals);
        return new(Enumerable.Range(0, q.To.DayNumber - q.From.DayNumber + 1).Select(q.From.AddDays)
            .Select(d => new TrendPoint(d, groups.GetValueOrDefault(d, Zero))).ToArray(), await Coverage(q, ct));
    }
    public async Task<AnalyticsPage<ProcedureStatistics>> Procedures(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var rows = (await Metrics(ward, q).ToArrayAsync(ct)).GroupBy(x => x.ProcedureId)
            .Select(g => new ProcedureStatistics(g.Key, Totals(g))).OrderByDescending(x => x.Totals.ReceivedCount).ThenBy(x => x.ProcedureId).ToArray();
        return new(rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArray(), q.Page, q.PageSize, rows.Length, await Coverage(q, ct));
    }
    public async Task<AnalyticsPage<OfficerStatistics>> Officers(string ward, AnalyticsQuery q, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var rows = (await db.Officers.AsNoTracking().Where(x => x.WardCode == ward && x.MetricDate >= q.From && x.MetricDate <= q.To &&
            (q.ProcedureId == null || x.ProcedureId == q.ProcedureId)).ToArrayAsync(ct)).GroupBy(x => x.OfficerId)
            .Select(g => new OfficerStatistics(g.Key, g.Sum(x => (long)x.ReadyCount), g.Sum(x => (long)x.RevisionRequestedCount),
                g.Sum(x => (long)x.DurationSampleCount), g.Sum(x => (long)x.DurationSampleCount) == 0 ? null :
                    g.Sum(x => x.TotalProcessingMinutes) / g.Sum(x => (long)x.DurationSampleCount)))
            .OrderByDescending(x => x.ReadyCount).ThenBy(x => x.OfficerId).ToArray();
        return new(rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArray(), q.Page, q.PageSize, rows.Length, await Coverage(q, ct));
    }
    public async Task<FeedbackDto> CreateFeedback(Guid citizen, Guid id, string token, CreateFeedback input, CancellationToken ct)
    {
        var application = await workflow.ReadOwned(id, token, ct);
        if (application.Id != id || application.UserId != citizen)
            throw new AnalyticsException(404, "feedback.application_not_found", "Không tìm thấy hồ sơ trong phạm vi của bạn.");
        if (application.Status != "DOCUMENTS_READY")
            throw new AnalyticsException(409, "feedback.not_ready", "Chỉ đánh giá sau khi hồ sơ đạt DOCUMENTS_READY.");
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
