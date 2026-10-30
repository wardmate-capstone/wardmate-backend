using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class ConsultationService(WorkflowDbContext db, IDispatchDirectory directory, IProcedureCatalogClient catalog, TimeProvider clock) : IConsultationService
{
    private static WorkflowResult<T> Fail<T>(int status, string code, string message) => WorkflowResult<T>.Fail(status, "consultation." + code, message);
    private static bool Text(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 4000;
    private static bool Scope(ReviewActor actor, ApplicationRecord a) => actor.CanReadWard && !string.IsNullOrWhiteSpace(actor.WardCode) && actor.WardCode == a.WardCode && actor.Id != a.UserId;
    public async Task<WorkflowResult<object>> Inbox(ReviewActor actor, int page, int pageSize, CancellationToken ct)
    {
        if (!actor.CanReadWard || string.IsNullOrWhiteSpace(actor.WardCode)) return Fail<object>(403, "forbidden", "Cần tài khoản cán bộ hoặc Manager có phường.");
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100) return Fail<object>(400, "invalid_page", "Trang không hợp lệ, pageSize tối đa 100.");
        var q = from c in db.Consultations.AsNoTracking() join a in db.Applications.AsNoTracking() on c.ApplicationId equals a.Id
                where a.WardCode == actor.WardCode && a.UserId != actor.Id && (actor.IsManager || c.SpecialistId == actor.Id)
                select c;
        return WorkflowResult<object>.Ok(new { items = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct), page, pageSize, total = await q.CountAsync(ct) });
    }
    public async Task<WorkflowResult<object>> DocumentAccess(ReviewActor actor, Guid applicationId, CancellationToken ct)
    {
        var app = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (app is null || !Scope(actor, app) || !actor.CanMutate || app.Status != "UNDER_REVIEW") return Fail<object>(403, "forbidden", "Không có quyền đọc tài liệu tham vấn.");
        var category = app.CategoryId;
        if (category is not > 0) category = (await catalog.Get(app.ProcedureId, ct)).Value?.CategoryId;
        if (category is not > 0) return Fail<object>(503, "category_unavailable", "Chưa xác minh lĩnh vực.");
        var version = await db.Versions.Where(x => x.ApplicationId == applicationId).MaxAsync(x => (int?)x.VersionNumber, ct);
        if (actor.AssignedCategories?.Contains(category.Value) != true || !await db.Consultations.AnyAsync(x => x.ApplicationId == applicationId && x.SpecialistId == actor.Id && x.Status != "CLOSED" && x.AssignmentVersion == app.AssignmentVersion && x.ApplicationVersion == version, ct))
            return Fail<object>(403, "forbidden", "Tham vấn đã đóng hoặc hồ sơ đã thay đổi.");
        return WorkflowResult<object>.Ok(new { allowed = true, applicationId, version });
    }
    public async Task<WorkflowResult<ApplicationConsultation[]>> List(ReviewActor actor, Guid applicationId, CancellationToken ct)
    {
        var app = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (app is null || !Scope(actor, app)) return Fail<ApplicationConsultation[]>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        return WorkflowResult<ApplicationConsultation[]>.Ok(await db.Consultations.AsNoTracking().Where(x => x.ApplicationId == applicationId &&
            (actor.IsManager || app.AssignedOfficerId == actor.Id || x.SpecialistId == actor.Id)).OrderBy(x => x.CreatedAtUtc).ToArrayAsync(ct));
    }
    public async Task<WorkflowResult<ApplicationConsultation>> Create(ReviewActor actor, Guid applicationId, ConsultationInput input, string token, CancellationToken ct)
    {
        if (!actor.IsManager || string.IsNullOrWhiteSpace(actor.WardCode)) return Fail<ApplicationConsultation>(403, "forbidden", "Chỉ Manager yêu cầu tham vấn.");
        if (!Text(input.Question) || input.SpecialistId == Guid.Empty || input.ExpectedAssignmentVersion < 0) return Fail<ApplicationConsultation>(400, "invalid_input", "Cần cán bộ, revision và câu hỏi tối đa 4000 ký tự.");
        var officers = await directory.Officers(token, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await StaffingLocks.Ward(db, actor.WardCode, ct);
        var app = await db.Applications.FromSqlInterpolated($"SELECT * FROM applications WHERE id = {applicationId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (app is null || !Scope(actor, app)) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        if (app.Status != "UNDER_REVIEW" || app.AssignedOfficerId is null || app.AssignmentVersion != input.ExpectedAssignmentVersion)
            return Fail<ApplicationConsultation>(409, "application_changed", "Cần hồ sơ đang xử lý và revision phân công mới nhất.");
        var category = app.CategoryId;
        if (category is not > 0) category = (await catalog.Get(app.ProcedureId, ct)).Value?.CategoryId;
        if (category is not > 0) return Fail<ApplicationConsultation>(503, "category_unavailable", "Chưa xác minh được lĩnh vực.");
        var officer = officers.SingleOrDefault(x => x.Id == input.SpecialistId);
        if (officer is null || !officer.IsActive || officer.WardCode != app.WardCode || !officer.Roles.Contains("FRONT_DESK_OFFICER") || officer.Roles.Contains("MANAGER") ||
            !officer.AssignedCategories.Contains(category.Value) || officer.Permissions?.Contains("workflow.consultation.respond") != true || officer.Id == app.UserId || officer.Id == app.AssignedOfficerId ||
            (await StaffingLocks.Availability(db, officer.Id, app.WardCode, clock.GetUtcNow().UtcDateTime, ct)).Length > 0)
            return Fail<ApplicationConsultation>(422, "specialist_ineligible", "Cần cán bộ khác đủ quyền, cùng phường/lĩnh vực và có lịch làm, không nghỉ.");
        if (await db.Consultations.AnyAsync(x => x.ApplicationId == applicationId && x.SpecialistId == input.SpecialistId && x.Status != "CLOSED", ct))
            return Fail<ApplicationConsultation>(409, "already_open", "Đã có tham vấn chưa đóng cho cán bộ này.");
        var version = await db.Versions.Where(x => x.ApplicationId == applicationId).MaxAsync(x => (int?)x.VersionNumber, ct);
        if (version is null) return Fail<ApplicationConsultation>(409, "snapshot_missing", "Hồ sơ thiếu phiên bản đã nộp.");
        app.CategoryId = category;
        var item = new ApplicationConsultation { ApplicationId = applicationId, SpecialistId = officer.Id, RequestedBy = actor.Id, ApplicationVersion = version.Value,
            AssignmentVersion = app.AssignmentVersion, Question = input.Question.Trim(), CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        db.Consultations.Add(item); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return WorkflowResult<ApplicationConsultation>.Ok(item);
    }
    public async Task<WorkflowResult<ApplicationConsultation>> Reply(ReviewActor actor, Guid applicationId, Guid id, ConsultationReply input, bool close, CancellationToken ct)
    {
        if (!Text(input.Text)) return Fail<ApplicationConsultation>(400, "invalid_text", "Cần nội dung tối đa 4000 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var app = await db.Applications.FromSqlInterpolated($"SELECT * FROM applications WHERE id = {applicationId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (app is null || !Scope(actor, app)) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        var item = await db.Consultations.SingleOrDefaultAsync(x => x.Id == id && x.ApplicationId == applicationId, ct);
        if (item is null) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy tham vấn.");
        if (close ? !actor.IsManager : !actor.CanMutate || item.SpecialistId != actor.Id) return Fail<ApplicationConsultation>(403, "forbidden", "Không có quyền thao tác tham vấn này.");
        if (item.Version != input.ExpectedVersion || item.Status == "CLOSED" || !close && item.Status != "OPEN") return Fail<ApplicationConsultation>(409, "version_conflict", "Tham vấn đã thay đổi. Tải lại dữ liệu.");
        if (!close && (app.Status != "UNDER_REVIEW" || app.AssignmentVersion != item.AssignmentVersion ||
            await db.Versions.Where(x => x.ApplicationId == applicationId).MaxAsync(x => (int?)x.VersionNumber, ct) != item.ApplicationVersion))
            return Fail<ApplicationConsultation>(409, "application_changed", "Hồ sơ hoặc phân công đã thay đổi; Manager cần tạo tham vấn mới.");
        if (!close && app.CategoryId is int category && actor.AssignedCategories?.Contains(category) != true)
            return Fail<ApplicationConsultation>(403, "category_mismatch", "Cán bộ không còn phụ trách lĩnh vực này.");
        if (close) { item.Status = "CLOSED"; item.CloseReason = input.Text.Trim(); item.ClosedAtUtc = clock.GetUtcNow().UtcDateTime; item.ClosedBy = actor.Id; }
        else { item.Status = "ANSWERED"; item.Answer = input.Text.Trim(); item.AnsweredAtUtc = clock.GetUtcNow().UtcDateTime; }
        item.Version++; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return WorkflowResult<ApplicationConsultation>.Ok(item);
    }
}
