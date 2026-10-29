using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class ApplicationReviewStore(WorkflowDbContext db) : IApplicationReviewStore
{
    public Task<ApplicationRecord?> Read(ReviewActor actor, Guid id, CancellationToken ct) => db.Applications.AsNoTracking()
        .Include(x => x.AssignmentHistory).Include(x => x.Versions).Include(x => x.Comments).Include(x => x.Checklists).Include(x => x.History).AsSplitQuery()
        .SingleOrDefaultAsync(x => x.Id == id && (x.UserId == actor.Id || actor.CanReadWard && actor.WardCode != null && actor.WardCode != "" && x.WardCode == actor.WardCode && x.UserId != actor.Id), ct);

    public async Task<ApplicationPage> Pending(ReviewActor actor, OfficerPendingInput input, CancellationToken ct)
    {
        if (!actor.CanReadWard || string.IsNullOrEmpty(actor.WardCode)) return new([], input.Page, input.PageSize, 0);
        var q = db.Applications.AsNoTracking().Where(x => x.WardCode == actor.WardCode && x.UserId != actor.Id
            && (x.Status == ApplicationStates.Submitted || x.Status == ApplicationStates.UnderReview)
            && (actor.IsManager || x.AssignedOfficerId == null || x.AssignedOfficerId == actor.Id));
        if (input.Status is not null) q = q.Where(x => x.Status == input.Status);
        if (input.ProcedureId is not null) q = q.Where(x => x.ProcedureId == input.ProcedureId);
        if (input.SubmittedFrom is not null) q = q.Where(x => x.SubmittedAt >= input.SubmittedFrom.Value.UtcDateTime);
        if (input.SubmittedTo is not null) q = q.Where(x => x.SubmittedAt <= input.SubmittedTo.Value.UtcDateTime);
        var categories = actor.AssignedCategories ?? [];
        if (!string.IsNullOrWhiteSpace(input.CategoryIds))
        {
            var requested = input.CategoryIds.Split(',').Select(int.Parse).ToArray();
            q = q.Where(x => x.CategoryId != null && requested.Contains(x.CategoryId.Value));
        }
        if (input.Scope == "my-categories" && !actor.IsManager && categories.Length > 0)
            q = q.Where(x => x.CategoryId != null && categories.Contains(x.CategoryId.Value));
        if (input.Assignee == "me") q = q.Where(x => x.AssignedOfficerId == actor.Id);
        else if (input.Assignee == "unassigned") q = q.Where(x => x.AssignedOfficerId == null);
        else if (Guid.TryParse(input.Assignee, out var target)) q = q.Where(x => x.AssignedOfficerId == target);
        if (input.Overdue == true) q = Overdue(q, DateTime.UtcNow);
        var total = await q.CountAsync(ct);
        var items = await Order(q, actor, input.Sort, true).Skip((input.Page - 1) * input.PageSize).Take(input.PageSize)
            .Select(x => new ApplicationSummaryDto(x.Id, x.ApplicationCode, x.ProcedureId, x.ProcedureTitle, x.Status, x.CreatedAt, x.SubmittedAt, x.WardCode, x.AssignedOfficerId, x.FirstSubmittedAtUtc, x.DueAtUtc, x.DocumentsReadyAt, x.DeadlineStatus, x.DeadlinePausedAtUtc, x.CategoryId, x.CategoryName, x.AssignedOfficerName, x.AssignedAt, x.ResubmitCount, x.AssignmentVersion, x.ApplicantName)).ToArrayAsync(ct);
        return new(items, input.Page, input.PageSize, total);
    }

    public async Task<ApplicationPage> ListAll(ReviewActor actor, OfficerListInput input, CancellationToken ct)
    {
        if (!actor.CanReadWard || string.IsNullOrEmpty(actor.WardCode)) return new([], input.Page, input.PageSize, 0);
        // Trả hồ sơ đã từng nộp thuộc phường — trừ hồ sơ của chính cán bộ đó (tránh lộ dữ liệu/xung đột).
        // Bản nháp và nháp công dân tự hủy (chưa từng nộp) không bao giờ xuất hiện, kể cả khi FE gửi Status=DRAFT.
        var q = db.Applications.AsNoTracking()
            .Where(x => x.WardCode == actor.WardCode && x.UserId != actor.Id
                && x.Status != ApplicationStates.Draft && x.SubmittedAt != null);
        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var rawStatuses = input.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant())
                .ToArray();
            if (rawStatuses.Length == 1)
            {
                var s0 = rawStatuses[0];
                q = q.Where(x => x.Status == s0);
            }
            else if (rawStatuses.Length > 1)
            {
                q = q.Where(x => rawStatuses.Contains(x.Status));
            }
        }
        if (input.ProcedureId is not null)
            q = q.Where(x => x.ProcedureId == input.ProcedureId);
        if (!string.IsNullOrWhiteSpace(input.ApplicationCode))
            q = q.Where(x => x.ApplicationCode != null && x.ApplicationCode.Contains(input.ApplicationCode));
        if (input.SubmittedFrom is not null)
            q = q.Where(x => x.SubmittedAt >= input.SubmittedFrom.Value.UtcDateTime);
        if (input.SubmittedTo is not null)
            q = q.Where(x => x.SubmittedAt <= input.SubmittedTo.Value.UtcDateTime);
        var categories = actor.AssignedCategories ?? [];
        if (!string.IsNullOrWhiteSpace(input.CategoryIds))
        {
            var requested = input.CategoryIds.Split(',').Select(int.Parse).ToArray();
            q = q.Where(x => x.CategoryId != null && requested.Contains(x.CategoryId.Value));
        }
        if (input.Scope == "my-categories" && !actor.IsManager && categories.Length > 0)
            q = q.Where(x => x.CategoryId != null && categories.Contains(x.CategoryId.Value));
        if (input.Assignee == "me") q = q.Where(x => x.AssignedOfficerId == actor.Id);
        else if (input.Assignee == "unassigned") q = q.Where(x => x.AssignedOfficerId == null);
        else if (Guid.TryParse(input.Assignee, out var target)) q = q.Where(x => x.AssignedOfficerId == target);
        if (input.Overdue == true) q = Overdue(q, DateTime.UtcNow);
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, input.Page);
        var pageSize = Math.Clamp(input.PageSize, 1, 100);
        var items = await Order(q, actor, input.Sort, false)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ApplicationSummaryDto(x.Id, x.ApplicationCode, x.ProcedureId, x.ProcedureTitle, x.Status, x.CreatedAt, x.SubmittedAt, x.WardCode, x.AssignedOfficerId, x.FirstSubmittedAtUtc, x.DueAtUtc, x.DocumentsReadyAt, x.DeadlineStatus, x.DeadlinePausedAtUtc, x.CategoryId, x.CategoryName, x.AssignedOfficerName, x.AssignedAt, x.ResubmitCount, x.AssignmentVersion, x.ApplicantName))
            .ToArrayAsync(ct);
        return new(items, page, pageSize, total);
    }

    // Same rule as Analytics openOverdue: a pause started after the due time stays overdue.
    private static IQueryable<ApplicationRecord> Overdue(IQueryable<ApplicationRecord> q, DateTime now) =>
        q.Where(x => x.DeadlineStatus == "KNOWN" && x.DueAtUtc != null && x.DueAtUtc < (x.DeadlinePausedAtUtc ?? now)
            && x.DocumentsReadyAt == null && (x.Status == "SUBMITTED" || x.Status == "UNDER_REVIEW" || x.Status == "NEED_REVISION"));

    public async Task<AssignmentSummaryDto> AssignmentSummary(ReviewActor actor, CancellationToken ct)
    {
        if (!actor.IsManager || string.IsNullOrEmpty(actor.WardCode)) return new(0, 0, []);
        var now = DateTime.UtcNow;
        var ward = db.Applications.AsNoTracking().Where(x => x.WardCode == actor.WardCode && x.UserId != actor.Id);
        var unassigned = await ward.CountAsync(x => x.Status == ApplicationStates.Submitted && x.AssignedOfficerId == null, ct);
        var overdue = await Overdue(ward, now).CountAsync(ct);
        // A resubmitted file returns to SUBMITTED but stays with its officer, so it still counts as that officer's work.
        var active = ward.Where(x => x.AssignedOfficerId != null && (x.Status == ApplicationStates.Submitted || x.Status == ApplicationStates.UnderReview
            || x.Status == ApplicationStates.NeedRevision || x.Status == ApplicationStates.DocumentsReady));
        var counts = await active.GroupBy(x => x.AssignedOfficerId!.Value).Select(g => new { OfficerId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var late = await Overdue(active, now).GroupBy(x => x.AssignedOfficerId!.Value).Select(g => new { OfficerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OfficerId, x => x.Count, ct);
        return new(unassigned, overdue, counts.OrderByDescending(x => x.Count).ThenBy(x => x.OfficerId)
            .Select(x => new OfficerWorkloadDto(x.OfficerId, x.Count, late.GetValueOrDefault(x.OfficerId))).ToArray());
    }

    public async Task<OfficerActivityPage> Activity(ReviewActor actor, Guid officerId, OfficerActivityInput input, CancellationToken ct)
    {
        if (!actor.CanReadWard || string.IsNullOrEmpty(actor.WardCode)) return new([], input.Page, input.PageSize, 0);
        // Only files of the caller's current ward, and never the officer's own citizen applications.
        var apps = db.Applications.AsNoTracking().Where(a => a.WardCode == actor.WardCode && a.UserId != officerId);
        var statuses = db.StatusHistory.AsNoTracking().Where(h => h.ChangedBy == officerId);
        var assignments = db.Set<ApplicationAssignmentHistory>().AsNoTracking().Where(h => h.ActorId == officerId);
        if (input.From is not null)
        {
            var from = input.From.Value.UtcDateTime;
            statuses = statuses.Where(h => h.CreatedAt >= from); assignments = assignments.Where(h => h.CreatedAt >= from);
        }
        if (input.To is not null)
        {
            var to = input.To.Value.UtcDateTime;
            statuses = statuses.Where(h => h.CreatedAt <= to); assignments = assignments.Where(h => h.CreatedAt <= to);
        }
        var action = input.Action;
        var assignmentAction = action is "CLAIM" or "RELEASE" or "ASSIGN" or "TRANSFER";
        if (action is not null) { statuses = statuses.Where(h => h.ToStatus == action); assignments = assignments.Where(h => h.Action == action); }
        var statusRows = statuses.Join(apps, h => h.ApplicationId, a => a.Id, (h, a) => new { h, a });
        var assignmentRows = assignments.Join(apps, h => h.ApplicationId, a => a.Id, (h, a) => new { h, a });
        var includeStatus = action is null || !assignmentAction;
        var includeAssignment = action is null || assignmentAction;
        var total = (includeStatus ? await statusRows.CountAsync(ct) : 0) + (includeAssignment ? await assignmentRows.CountAsync(ct) : 0);
        // Both sources are already newest-first; take enough of each to fill the page, then merge (stable sort keeps DB tie order).
        var take = input.Page * input.PageSize;
        var items = new List<OfficerActivityDto>();
        if (includeStatus) items.AddRange(await statusRows.OrderByDescending(x => x.h.CreatedAt).ThenBy(x => x.h.Id).Take(take)
            .Select(x => new OfficerActivityDto(x.h.Id, "STATUS", x.h.ToStatus, x.a.Id, x.a.ApplicationCode, x.a.ProcedureTitle, x.a.ApplicantName, x.h.Reason, x.h.CreatedAt)).ToListAsync(ct));
        if (includeAssignment) items.AddRange(await assignmentRows.OrderByDescending(x => x.h.CreatedAt).ThenBy(x => x.h.Id).Take(take)
            .Select(x => new OfficerActivityDto(x.h.Id, "ASSIGNMENT", x.h.Action, x.a.Id, x.a.ApplicationCode, x.a.ProcedureTitle, x.a.ApplicantName, x.h.Reason, x.h.CreatedAt)).ToListAsync(ct));
        return new(items.OrderByDescending(x => x.CreatedAt).Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).ToArray(), input.Page, input.PageSize, total);
    }

    private static IOrderedQueryable<ApplicationRecord> Order(IQueryable<ApplicationRecord> q, ReviewActor actor, string? sort, bool pending)
    {
        if (sort == "submittedAt") return q.OrderBy(x => x.SubmittedAt == null).ThenBy(x => x.SubmittedAt).ThenBy(x => x.Id);
        if (sort == "-submittedAt") return q.OrderBy(x => x.SubmittedAt == null).ThenByDescending(x => x.SubmittedAt).ThenBy(x => x.Id);
        if (sort == "dueAt") return q.OrderBy(x => x.DueAtUtc == null).ThenBy(x => x.DueAtUtc).ThenBy(x => x.Id);
        var categories = actor.AssignedCategories ?? [];
        var ordered = q.OrderByDescending(x => !actor.IsManager && categories.Length > 0 && x.CategoryId != null && categories.Contains(x.CategoryId.Value));
        return pending ? ordered.ThenBy(x => x.SubmittedAt).ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.UpdatedAt).ThenBy(x => x.Id);
    }
    public async Task<WorkflowResult<ReviewResultDto>> Mutate(Guid id, Func<ApplicationRecord, WorkflowResult<ReviewResultDto>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var a = await db.Applications.FromSqlInterpolated($"SELECT * FROM applications WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (a is null) return WorkflowResult<ReviewResultDto>.Fail(404, "application.not_found", "Không tìm thấy hồ sơ.");
        await db.Entry(a).Collection(x => x.Checklists).LoadAsync(ct);
        await db.Entry(a).Collection(x => x.History).LoadAsync(ct);
        await db.Entry(a).Collection(x => x.Versions).LoadAsync(ct);
        await db.Entry(a).Collection(x => x.Comments).LoadAsync(ct);
        await db.Entry(a).Collection(x => x.AssignmentHistory).LoadAsync(ct); var assignments = a.AssignmentHistory.Select(x => x.Id).ToHashSet(); var histories = a.History.Select(x => x.Id).ToHashSet();
        var versions = a.Versions.Select(x => x.Id).ToHashSet();
        var comments = a.Comments.Select(x => x.Id).ToHashSet();
        var result = action(a);
        if (result.Error is not null) return result;
        db.Set<ApplicationAssignmentHistory>().AddRange(a.AssignmentHistory.Where(x => !assignments.Contains(x.Id))); db.StatusHistory.AddRange(a.History.Where(x => !histories.Contains(x.Id)));
        db.Versions.AddRange(a.Versions.Where(x => !versions.Contains(x.Id)));
        db.Comments.AddRange(a.Comments.Where(x => !comments.Contains(x.Id)));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}



