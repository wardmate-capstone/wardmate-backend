using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class ApplicationStore(WorkflowDbContext db) : IApplicationStore
{
    public async Task Add(ApplicationRecord application, CancellationToken ct)
    {
        // EF saves the application, checklist batch and initial history atomically.
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
    }
    public Task<ApplicationRecord?> Get(Guid userId, Guid applicationId, CancellationToken ct) =>
        db.Applications.AsNoTracking().Include(x => x.Checklists).Include(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == applicationId && x.UserId == userId, ct);

    public async Task<ApplicationPage> List(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var source = db.Applications.AsNoTracking().Where(x => x.UserId == userId);
        var count = await source.CountAsync(ct);
        var items = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ApplicationSummaryDto(x.Id, x.ApplicationCode, x.ProcedureId,
                x.ProcedureTitle, x.Status, x.CreatedAt, x.SubmittedAt)).ToArrayAsync(ct);
        return new(items, page, pageSize, count);
    }
    public async Task<WorkflowResult<ApplicationDto>> WithLock(Guid userId, Guid applicationId,
        Func<ApplicationRecord, Task<WorkflowResult<ApplicationDto>>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var application = await db.Applications.FromSqlInterpolated(
            $"SELECT * FROM applications WHERE id = {applicationId} AND user_id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (application is null) return WorkflowResult<ApplicationDto>.Fail(404, "application.not_found", "Không tìm thấy hồ sơ của bạn.");
        await db.Entry(application).Collection(x => x.Checklists).LoadAsync(ct);
        await db.Entry(application).Collection(x => x.History).LoadAsync(ct);
        var existingHistoryIds = application.History.Select(x => x.Id).ToHashSet();
        var result = await operation(application);
        if (result.Error is not null) return result;
        // New history has an application-generated Guid, so explicitly mark it as an INSERT.
        db.StatusHistory.AddRange(application.History.Where(x => !existingHistoryIds.Contains(x.Id)));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
    public async Task<string> NextCode(DateTime now, CancellationToken ct)
    {
        var sequence = await db.Database.SqlQueryRaw<long>("SELECT nextval('application_code_sequence') AS \"Value\"").SingleAsync(ct);
        return $"HS-{now.AddHours(7):yyyyMMdd}-{sequence:D8}";
    }
}
