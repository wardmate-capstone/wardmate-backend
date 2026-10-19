using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class ApplicationReviewStore(WorkflowDbContext db) : IApplicationReviewStore
{
    public Task<ApplicationRecord?> Read(ReviewActor actor, Guid id, CancellationToken ct) => db.Applications.AsNoTracking()
        .Include(x => x.Versions).Include(x => x.Comments).Include(x => x.Checklists).Include(x => x.History).AsSplitQuery()
        .SingleOrDefaultAsync(x => x.Id == id && (x.UserId == actor.Id || actor.IsOfficer && actor.WardCode != null && x.WardCode == actor.WardCode && x.AssignedOfficerId == actor.Id), ct);

    public async Task<ApplicationPage> Pending(ReviewActor actor, OfficerPendingInput input, CancellationToken ct)
    {
        if (!actor.IsOfficer || string.IsNullOrEmpty(actor.WardCode)) return new([], input.Page, input.PageSize, 0);
        var q = db.Applications.AsNoTracking().Where(x => x.WardCode == actor.WardCode && x.UserId != actor.Id
            && (x.Status == ApplicationStates.Submitted || x.Status == ApplicationStates.UnderReview)
            && (x.AssignedOfficerId == null || x.AssignedOfficerId == actor.Id));
        if (input.Status is not null) q = q.Where(x => x.Status == input.Status);
        if (input.ProcedureId is not null) q = q.Where(x => x.ProcedureId == input.ProcedureId);
        if (input.SubmittedFrom is not null) q = q.Where(x => x.SubmittedAt >= input.SubmittedFrom.Value.UtcDateTime);
        if (input.SubmittedTo is not null) q = q.Where(x => x.SubmittedAt <= input.SubmittedTo.Value.UtcDateTime);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Skip((input.Page - 1) * input.PageSize).Take(input.PageSize)
            .Select(x => new ApplicationSummaryDto(x.Id, x.ApplicationCode, x.ProcedureId, x.ProcedureTitle, x.Status, x.CreatedAt, x.SubmittedAt, x.WardCode)).ToArrayAsync(ct);
        return new(items, input.Page, input.PageSize, total);
    }

    public async Task<ApplicationPage> ListAll(ReviewActor actor, OfficerListInput input, CancellationToken ct)
    {
        if (!actor.IsOfficer || string.IsNullOrEmpty(actor.WardCode)) return new([], input.Page, input.PageSize, 0);
        // Trả toàn bộ hồ sơ thuộc phường — trừ hồ sơ của chính cán bộ đó (tránh lộ dữ liệu/xung đột)
        var q = db.Applications.AsNoTracking()
            .Where(x => x.WardCode == actor.WardCode && x.UserId != actor.Id);
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
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, input.Page);
        var pageSize = Math.Clamp(input.PageSize, 1, 100);
        var items = await q
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ApplicationSummaryDto(x.Id, x.ApplicationCode, x.ProcedureId, x.ProcedureTitle, x.Status, x.CreatedAt, x.SubmittedAt, x.WardCode))
            .ToArrayAsync(ct);
        return new(items, page, pageSize, total);
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
        var histories = a.History.Select(x => x.Id).ToHashSet();
        var versions = a.Versions.Select(x => x.Id).ToHashSet();
        var comments = a.Comments.Select(x => x.Id).ToHashSet();
        var result = action(a);
        if (result.Error is not null) return result;
        db.StatusHistory.AddRange(a.History.Where(x => !histories.Contains(x.Id)));
        db.Versions.AddRange(a.Versions.Where(x => !versions.Contains(x.Id)));
        db.Comments.AddRange(a.Comments.Where(x => !comments.Contains(x.Id)));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}


