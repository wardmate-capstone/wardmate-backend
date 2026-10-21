using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class AnalyticsAggregationJob(AnalyticsDbContext db, IAnalyticsWorkflowClient workflow,
    IConfiguration config, TimeProvider clock, ILogger<AnalyticsAggregationJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public Task Refresh(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var days = config.GetValue("Analytics:LookbackDays", 7);
        return Rebuild(today.AddDays(1 - days), today, ct);
    }
    [AutomaticRetry(Attempts = 3)]
    public async Task Rebuild(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (from < new DateOnly(2000, 1, 1) || from > to || to > today || to.DayNumber - from.DayNumber >= 366)
            throw new ArgumentException("Khoảng tổng hợp phải từ 1 đến 366 ngày, không nằm trong tương lai.");
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Cross-replica transaction lock, including the source read: an older response cannot overwrite a newer one.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(313233, {date.DayNumber})", ct);
            var source = await workflow.ReadDay(date, ct);
            Validate(source, date);
            await db.Procedures.Where(x => x.MetricDate == date).ExecuteDeleteAsync(ct);
            await db.Officers.Where(x => x.MetricDate == date).ExecuteDeleteAsync(ct);
            db.Procedures.AddRange(source.Procedures.Select(x => new ProcedureMetric { MetricDate = date,
                WardCode = x.WardCode, ProcedureId = x.ProcedureId, ReceivedCount = x.ReceivedCount, ReadyCount = x.ReadyCount,
                RevisionRequestedCount = x.RevisionRequestedCount, ResubmittedCount = x.ResubmittedCount,
                CancelledCount = x.CancelledCount, LegacyApprovedCount = x.LegacyApprovedCount, LegacyRejectedCount = x.LegacyRejectedCount }));
            db.Officers.AddRange(source.Officers.Select(x => new OfficerMetric { MetricDate = date,
                WardCode = x.WardCode, ProcedureId = x.ProcedureId, OfficerId = x.OfficerId, ReadyCount = x.ReadyCount,
                RevisionRequestedCount = x.RevisionRequestedCount, DurationSampleCount = x.DurationSampleCount,
                TotalProcessingMinutes = x.TotalProcessingMinutes }));
            var refresh = await db.Refreshes.SingleOrDefaultAsync(x => x.MetricDate == date, ct);
            if (refresh is null) { refresh = new() { MetricDate = date }; db.Refreshes.Add(refresh); }
            refresh.SourceAtUtc = source.GeneratedAtUtc;
            refresh.RefreshedAtUtc = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
            logger.LogInformation("Analytics rebuilt {Date}: {ProcedureGroups} procedure groups, {OfficerGroups} officer groups",
                date, source.Procedures.Length, source.Officers.Length);
        }
    }
    private static void Validate(AnalyticsDay s, DateOnly date)
    {
        if (s.ContractVersion != 1 || s.Date != date || s.TimeZone != "Asia/Ho_Chi_Minh" ||
            s.GeneratedAtUtc.Kind != DateTimeKind.Utc || s.Procedures is null || s.Officers is null ||
            s.Procedures.Any(x => x is null || x.ProcedureId == Guid.Empty || x.WardCode is null || x.WardCode.Length > 50 ||
                new[] { x.ReceivedCount, x.ReadyCount, x.RevisionRequestedCount, x.ResubmittedCount, x.CancelledCount, x.LegacyApprovedCount, x.LegacyRejectedCount }.Any(n => n < 0)) ||
            s.Officers.Any(x => x is null || x.OfficerId == Guid.Empty || x.ProcedureId == Guid.Empty || x.WardCode is null || x.WardCode.Length > 50 ||
                x.ReadyCount < 0 || x.RevisionRequestedCount < 0 || x.DurationSampleCount < 0 || x.DurationSampleCount > x.ReadyCount ||
                !double.IsFinite(x.TotalProcessingMinutes) || x.TotalProcessingMinutes < 0) ||
            s.Procedures.Select(x => (x.WardCode, x.ProcedureId)).Distinct().Count() != s.Procedures.Length ||
            s.Officers.Select(x => (x.WardCode, x.ProcedureId, x.OfficerId)).Distinct().Count() != s.Officers.Length)
            throw new InvalidOperationException("Workflow analytics contract is invalid; previous metrics preserved.");
    }
}
