using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class DispatchService(WorkflowDbContext db, IDispatchDirectory directory, IProcedureCatalogClient catalog, TimeProvider clock) : IDispatchService
{
    private static bool Manager(ReviewActor a) => a.IsManager && !string.IsNullOrWhiteSpace(a.WardCode);
    private static WorkflowResult<T> Fail<T>(int status, string code, string message) => WorkflowResult<T>.Fail(status, "dispatch." + code, message);
    private IQueryable<ApplicationRecord> Scope(ReviewActor actor) => db.Applications.Where(x => x.WardCode == actor.WardCode && x.UserId != actor.Id);
    public async Task<WorkflowResult<object>> List(ReviewActor actor, int page, int pageSize, CancellationToken ct)
    {
        if (!Manager(actor)) return Fail<object>(403, "forbidden", "Cần Manager có phường quản lý.");
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100) return Fail<object>(400, "invalid_page", "Trang không hợp lệ; pageSize từ 1 đến 100.");
        var q = Scope(actor).AsNoTracking();
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new DispatchApplication(x.Id, x.ApplicationCode, x.ProcedureId, x.ProcedureTitle, x.Status, x.AssignedOfficerId, x.AssignmentVersion, x.CategoryId)).ToArrayAsync(ct);
        return WorkflowResult<object>.Ok(new { items, page, pageSize, total });
    }
    private async Task<int?> Category(ApplicationRecord a, CancellationToken ct)
    {
        if (a.CategoryId is > 0) return a.CategoryId;
        var result = await catalog.Get(a.ProcedureId, ct);
        return result.Error is null && result.Value?.CategoryId is > 0 ? result.Value.CategoryId : null;
    }
    private static string[] Reasons(ApplicationRecord app, DispatchOfficer officer, int category) =>
        new[] { !officer.IsActive ? "ACCOUNT_INACTIVE" : null, officer.WardCode != app.WardCode ? "WARD_MISMATCH" : null,
            !officer.Roles.Contains("FRONT_DESK_OFFICER") || officer.Roles.Contains("MANAGER") ? "ROLE_MISMATCH" : null,
            !officer.AssignedCategories.Contains(category) ? "CATEGORY_MISMATCH" : null,
            officer.Permissions?.Contains("workflow.assign") != true ? "ASSIGN_PERMISSION_MISSING" : null,
            officer.Id == app.UserId ? "APPLICATION_OWNER" : null }.OfType<string>().ToArray();
    public async Task<WorkflowResult<DispatchCandidate[]>> Candidates(ReviewActor actor, Guid id, string token, CancellationToken ct)
    {
        if (!Manager(actor)) return Fail<DispatchCandidate[]>(403, "forbidden", "Cần Manager có phường quản lý.");
        var a = await Scope(actor).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Fail<DispatchCandidate[]>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        var category = await Category(a, ct);
        if (category is null) return Fail<DispatchCandidate[]>(503, "category_unavailable", "Chưa xác định được lĩnh vực hồ sơ từ Catalog.");
        var officers = await directory.Officers(token, ct);
        var counts = await db.Applications.Where(x => x.WardCode == actor.WardCode && x.AssignedOfficerId != null &&
            (x.Status == "SUBMITTED" || x.Status == "UNDER_REVIEW" || x.Status == "NEED_REVISION" || x.Status == "DOCUMENTS_READY"))
            .GroupBy(x => x.AssignedOfficerId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var candidates = new List<DispatchCandidate>();
        foreach (var officer in officers)
        {
            var reasons = Reasons(a, officer, category.Value).Concat(await StaffingLocks.Availability(db, officer.Id, a.WardCode, clock.GetUtcNow().UtcDateTime, ct)).ToArray();
            candidates.Add(new(officer.Id, officer.FullName, reasons.Length == 0, counts.GetValueOrDefault(officer.Id), reasons));
        }
        return WorkflowResult<DispatchCandidate[]>.Ok(candidates.OrderByDescending(x => x.Eligible).ThenBy(x => x.ActiveApplicationCount).ThenBy(x => x.OfficerId).ToArray());
    }
    public async Task<WorkflowResult<AssignmentRecord>> Assign(ReviewActor actor, Guid id, DispatchAssignmentInput input, string token, CancellationToken ct)
    {
        if (!Manager(actor)) return Fail<AssignmentRecord>(403, "forbidden", "Cần Manager có phường quản lý.");
        if (input.OfficerId == Guid.Empty || input.ExpectedAssignmentVersion < 0 || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000 ||
            input.ReasonCode is not ("INITIAL_ASSIGNMENT" or "OFFICER_LEAVE" or "WORKLOAD_BALANCE" or "SPECIALIST_REQUIRED"))
            return Fail<AssignmentRecord>(400, "invalid_input", "Cần cán bộ, revision và lý do hợp lệ tối đa 2000 ký tự.");
        // Fetch authoritative IAM data for each write; never trust candidate data supplied by FE.
        var officers = await directory.Officers(token, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await StaffingLocks.Ward(db, actor.WardCode!, ct);
        var a = await db.Applications.FromSqlInterpolated($"SELECT * FROM applications WHERE id = {id} FOR UPDATE").Include(x => x.AssignmentHistory).Include(x => x.History).SingleOrDefaultAsync(ct);
        if (a is null || a.WardCode != actor.WardCode || a.UserId == actor.Id) return Fail<AssignmentRecord>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        if (a.AssignmentVersion != input.ExpectedAssignmentVersion) return Fail<AssignmentRecord>(409, "version_conflict", "Phân công đã thay đổi. Tải lại hồ sơ.");
        if (a.Status is not ("SUBMITTED" or "UNDER_REVIEW" or "NEED_REVISION" or "DOCUMENTS_READY"))
            return Fail<AssignmentRecord>(409, "invalid_status", "Trạng thái hồ sơ không cho phép phân công.");
        if (!await db.Versions.AnyAsync(x => x.ApplicationId == id, ct)) return Fail<AssignmentRecord>(409, "snapshot_missing", "Hồ sơ chưa có phiên bản đã nộp.");
        if (a.AssignedOfficerId == input.OfficerId) return Fail<AssignmentRecord>(409, "already_assigned", "Cán bộ đã phụ trách hồ sơ này.");
        var category = await Category(a, ct);
        if (category is null) return Fail<AssignmentRecord>(503, "category_unavailable", "Chưa xác minh được lĩnh vực hồ sơ.");
        var officer = officers.SingleOrDefault(x => x.Id == input.OfficerId);
        if (officer is null || Reasons(a, officer, category.Value).Length > 0)
            return Fail<AssignmentRecord>(422, "officer_ineligible", "Cán bộ phải đang hoạt động, đúng phường, vai trò và lĩnh vực, không phải chủ hồ sơ.");
        if ((await StaffingLocks.Availability(db, officer.Id, a.WardCode, clock.GetUtcNow().UtcDateTime, ct)).Length > 0)
            return Fail<AssignmentRecord>(422, "officer_unavailable", "Cán bộ cần có ca làm hiện tại và không trong thời gian nghỉ đã duyệt.");

        var now = clock.GetUtcNow().UtcDateTime;
        var initial = a.Status == ApplicationStates.Submitted && a.AssignedOfficerId is null;
        if (initial)
        {
            a.History.Add(new ApplicationStatusHistory { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = ApplicationStates.UnderReview, ChangedBy = actor.Id, CreatedAt = now });
            a.Status = ApplicationStates.UnderReview;
        }
        var reasonText = $"[{input.ReasonCode}] {input.Reason.Trim()}";
        if (reasonText.Length > 500) reasonText = reasonText[..500];
        AssignmentHandlers.Record(a, initial ? "ASSIGN" : "TRANSFER", actor.Id, officer.Id, officer.FullName, reasonText, now);
        a.CategoryId = category;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);

        var h = a.AssignmentHistory.Last();
        var record = new AssignmentRecord
        {
            Id = h.Id,
            ApplicationId = a.Id,
            FromOfficerId = h.FromOfficerId,
            ToOfficerId = h.ToOfficerId ?? officer.Id,
            ActorId = actor.Id,
            Version = a.AssignmentVersion,
            ReasonCode = input.ReasonCode,
            Reason = input.Reason.Trim(),
            CreatedAt = h.CreatedAt,
            Action = h.Action
        };
        return WorkflowResult<AssignmentRecord>.Ok(record);
    }
    public async Task<WorkflowResult<AssignmentRecord[]>> History(ReviewActor actor, Guid id, CancellationToken ct)
    {
        if (!Manager(actor)) return Fail<AssignmentRecord[]>(403, "forbidden", "Cần Manager có phường quản lý.");
        if (!await Scope(actor).AnyAsync(x => x.Id == id, ct)) return Fail<AssignmentRecord[]>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        var raw = await db.AssignmentHistory.AsNoTracking().Where(x => x.ApplicationId == id).OrderBy(x => x.CreatedAt).ToArrayAsync(ct);
        var records = new List<AssignmentRecord>();
        for (var i = 0; i < raw.Length; i++)
        {
            var h = raw[i];
            var reasonCode = h.Action;
            var reason = h.Reason ?? string.Empty;
            if (reason.StartsWith('[') && reason.IndexOf(']') is var idx && idx > 1)
            {
                reasonCode = reason[1..idx];
                reason = reason[(idx + 1)..].TrimStart();
            }
            records.Add(new AssignmentRecord
            {
                Id = h.Id,
                ApplicationId = h.ApplicationId,
                FromOfficerId = h.FromOfficerId,
                ToOfficerId = h.ToOfficerId ?? Guid.Empty,
                ActorId = h.ActorId,
                Version = i + 1,
                ReasonCode = reasonCode,
                Reason = reason,
                CreatedAt = h.CreatedAt,
                Action = h.Action
            });
        }
        return WorkflowResult<AssignmentRecord[]>.Ok(records.ToArray());
    }
}
