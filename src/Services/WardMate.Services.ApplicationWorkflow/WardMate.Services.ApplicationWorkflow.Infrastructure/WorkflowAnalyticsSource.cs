using System.Data;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class WorkflowAnalyticsSource(WorkflowDbContext db, TimeProvider clock) : IWorkflowAnalyticsSource
{
    public async Task<AnalyticsDay> ReadDay(DateOnly date, CancellationToken ct)
    {
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).UtcDateTime;
        var end = start.AddDays(1);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        // Use history, not SubmittedAt: SubmittedAt is overwritten on each resubmission.
        var rows = await (from h in db.StatusHistory.AsNoTracking()
            join a in db.Applications.AsNoTracking() on h.ApplicationId equals a.Id
            where h.CreatedAt >= start && h.CreatedAt < end && h.FromStatus != h.ToStatus
            select new { h.ApplicationId, h.FromStatus, h.ToStatus, h.ChangedBy, h.CreatedAt,
                a.ProcedureId, Ward = a.WardCode ?? "",
                FirstSubmitted = a.History.Where(x => x.ToStatus == "SUBMITTED")
                    .Select(x => (DateTime?)x.CreatedAt).Min() })
            .Take(100001).ToArrayAsync(ct);
        if (rows.Length > 100000) throw new InvalidOperationException("analytics.daily_source_limit");
        var procedures = rows.GroupBy(x => new { x.ProcedureId, x.Ward }).Select(g => new ProcedureDay(
            g.Key.ProcedureId, g.Key.Ward,
            g.Where(x => x.ToStatus == "SUBMITTED" && x.FromStatus == "DRAFT").Select(x => x.ApplicationId).Distinct().Count(),
            g.Where(x => x.ToStatus == "DOCUMENTS_READY").Select(x => x.ApplicationId).Distinct().Count(),
            g.Count(x => x.ToStatus == "NEED_REVISION"),
            g.Count(x => x.ToStatus == "SUBMITTED" && x.FromStatus == "NEED_REVISION"),
            g.Where(x => x.ToStatus == "CANCELLED").Select(x => x.ApplicationId).Distinct().Count(),
            g.Where(x => x.ToStatus == "APPROVED").Select(x => x.ApplicationId).Distinct().Count(),
            g.Where(x => x.ToStatus == "REJECTED").Select(x => x.ApplicationId).Distinct().Count())).ToArray();
        var officers = rows.Where(x => x.ChangedBy != Guid.Empty && x.ToStatus is "DOCUMENTS_READY" or "NEED_REVISION")
            .GroupBy(x => new { x.ChangedBy, x.ProcedureId, x.Ward }).Select(g =>
            {
                var samples = g.Where(x => x.ToStatus == "DOCUMENTS_READY" && x.FirstSubmitted.HasValue && x.CreatedAt >= x.FirstSubmitted)
                    .GroupBy(x => x.ApplicationId).Select(x => x.OrderBy(y => y.CreatedAt).First()).ToArray();
                return new OfficerDay(g.Key.ChangedBy, g.Key.ProcedureId, g.Key.Ward,
                    g.Where(x => x.ToStatus == "DOCUMENTS_READY").Select(x => x.ApplicationId).Distinct().Count(),
                    g.Count(x => x.ToStatus == "NEED_REVISION"), samples.Length,
                    samples.Sum(x => (x.CreatedAt - x.FirstSubmitted!.Value).TotalMinutes));
            }).ToArray();
        await tx.CommitAsync(ct);
        return new(1, date, clock.GetUtcNow().UtcDateTime, "Asia/Ho_Chi_Minh", procedures, officers);
    }
}
