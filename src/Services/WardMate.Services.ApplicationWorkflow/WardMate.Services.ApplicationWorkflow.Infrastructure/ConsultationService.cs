using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class ConsultationService(WorkflowDbContext db, IDispatchDirectory directory, IProcedureCatalogClient catalog, TimeProvider clock) : IConsultationService
{
    private static WorkflowResult<T> Fail<T>(int status, string code, string message) => WorkflowResult<T>.Fail(status, "consultation." + code, message);
    private static bool Text(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 4000;
    private static bool Scope(ReviewActor actor, ApplicationRecord a) => actor.CanReadWard && !string.IsNullOrWhiteSpace(actor.WardCode) && actor.WardCode == a.WardCode && actor.Id != a.UserId;
    public async Task<WorkflowResult<object>> Inbox(ReviewActor actor, int page, int pageSize, CancellationToken ct, string? status = null)
    {
        if (!actor.CanReadWard || string.IsNullOrWhiteSpace(actor.WardCode)) return Fail<object>(403, "forbidden", "Cần tài khoản cán bộ hoặc Manager có phường.");
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100) return Fail<object>(400, "invalid_page", "Trang không hợp lệ, pageSize tối đa 100.");
        var q = from c in db.Consultations.AsNoTracking() join a in db.Applications.AsNoTracking() on c.ApplicationId equals a.Id
                where a.WardCode == actor.WardCode && a.UserId != actor.Id && (actor.IsManager || c.SpecialistId == actor.Id || c.RequestedBy == actor.Id) && (status == null || c.Status == status)
                select c;
        return WorkflowResult<object>.Ok(new { items = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct), page, pageSize, total = await q.CountAsync(ct) });
    }
    private Task<ApplicationRecord?> LockedApplication(Guid id, CancellationToken ct) =>
        db.Applications.FromSqlInterpolated($"SELECT * FROM applications WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);

    private Task<int?> LatestVersion(Guid id, CancellationToken ct) =>
        db.Versions.Where(x => x.ApplicationId == id).MaxAsync(x => (int?)x.VersionNumber, ct);

    private async Task<int?> Category(ApplicationRecord app, CancellationToken ct) =>
        app.CategoryId is > 0 ? app.CategoryId : (await catalog.Get(app.ProcedureId, ct)).Value?.CategoryId;

    private async Task<string[]> SpecialistReasons(ApplicationRecord app, DispatchOfficer officer, int category, CancellationToken ct)
    {
        var reasons = DispatchService.Reasons(app, officer, category, "workflow.consultation.respond").ToList();
        if (app.AssignedOfficerId == officer.Id) reasons.Add("CURRENT_ASSIGNEE");
        reasons.AddRange(await StaffingLocks.Availability(db, officer.Id, app.WardCode!, clock.GetUtcNow().UtcDateTime, ct));
        return reasons.ToArray();
    }

    public async Task<WorkflowResult<SpecialistCandidate[]>> Candidates(ReviewActor actor, Guid applicationId, string token, CancellationToken ct)
    {
        if (!actor.IsManager) return Fail<SpecialistCandidate[]>(403, "forbidden", "Cần quyền điều phối.");
        var app = await db.Applications.AsNoTracking().SingleOrDefaultAsync(a => a.Id == applicationId, ct);
        if (app == null || !Scope(actor, app)) return Fail<SpecialistCandidate[]>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        var category = await Category(app, ct);
        if (category is not > 0) return Fail<SpecialistCandidate[]>(503, "category_unavailable", "Chưa xác minh lĩnh vực.");
        var candidates = new List<SpecialistCandidate>();
        foreach (var officer in (await directory.Officers(token, ct)).Where(o => o.WardCode == app.WardCode))
        {
            var reasons = await SpecialistReasons(app, officer, category.Value, ct);
            candidates.Add(new(officer.Id, officer.FullName, officer.AssignedCategories, reasons.Length == 0, reasons));
        }
        return WorkflowResult<SpecialistCandidate[]>.Ok(candidates.OrderByDescending(c => c.Eligible).ThenBy(c => c.OfficerId).ToArray());
    }

    public async Task<WorkflowResult<ApplicationConsultation>> Request(ReviewActor actor, Guid applicationId, ConsultationRequest input, CancellationToken ct)
    {
        if (!Text(input.Question) || input.ExpectedAssignmentVersion < 0) return Fail<ApplicationConsultation>(400, "invalid_input", "Cần câu hỏi tối đa 4000 ký tự và phiên bản phân công.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var app = await LockedApplication(applicationId, ct);
        if (app == null || !Scope(actor, app)) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        if (!actor.CanMutate || app.AssignedOfficerId != actor.Id) return Fail<ApplicationConsultation>(403, "forbidden", "Chỉ cán bộ phụ trách được yêu cầu hỗ trợ.");
        var snapshot = await LatestVersion(applicationId, ct);
        if (app.Status != "UNDER_REVIEW" || app.AssignmentVersion != input.ExpectedAssignmentVersion || snapshot == null)
            return Fail<ApplicationConsultation>(409, "application_changed", "Hãy tải lại hồ sơ đang xử lý.");
        if (await db.Consultations.AnyAsync(c => c.ApplicationId == applicationId && c.AssignmentVersion == app.AssignmentVersion && c.Status == "PENDING_DISPATCH", ct))
            return Fail<ApplicationConsultation>(409, "already_pending", "Hồ sơ đã có yêu cầu chờ điều phối.");
        var item = new ApplicationConsultation { ApplicationId = applicationId, ApplicationCode = app.ApplicationCode, RequestedBy = actor.Id,
            RequestedByName = actor.FullName, ApplicationVersion = snapshot.Value, AssignmentVersion = app.AssignmentVersion,
            Question = input.Question.Trim(), CreatedAtUtc = clock.GetUtcNow().UtcDateTime, Status = "PENDING_DISPATCH" };
        db.Consultations.Add(item);
        WorkflowOutboxCapture.Staffing(db, app.WardCode!, item.Id, actor.Id, "CONSULTATION_REQUESTED", true, item.CreatedAtUtc);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return WorkflowResult<ApplicationConsultation>.Ok(item);
    }

    public async Task<WorkflowResult<ApplicationConsultation>> Dispatch(ReviewActor actor, Guid applicationId, Guid id, ConsultationDispatch input, string token, CancellationToken ct)
    {
        if (!actor.IsManager || string.IsNullOrWhiteSpace(actor.WardCode)) return Fail<ApplicationConsultation>(403, "forbidden", "Cần Manager.");
        if (input.SpecialistId == Guid.Empty || input.ExpectedVersion < 0) return Fail<ApplicationConsultation>(400, "invalid_input", "Cần cán bộ và phiên bản hợp lệ.");
        var officers = await directory.Officers(token, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StaffingLocks.Ward(db, actor.WardCode, ct);
        var app = await LockedApplication(applicationId, ct);
        if (app == null || !Scope(actor, app)) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        var item = await db.Consultations.SingleOrDefaultAsync(c => c.ApplicationId == applicationId && c.Id == id, ct);
        if (item == null) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy tham vấn.");
        if (item.Status != "PENDING_DISPATCH" || item.Version != input.ExpectedVersion || app.Status != "UNDER_REVIEW" ||
            app.AssignedOfficerId != item.RequestedBy || app.AssignmentVersion != item.AssignmentVersion || await LatestVersion(applicationId, ct) != item.ApplicationVersion)
            return Fail<ApplicationConsultation>(409, "application_changed", "Hồ sơ hoặc yêu cầu đã thay đổi.");
        var category = await Category(app, ct);
        if (category is not > 0) return Fail<ApplicationConsultation>(503, "category_unavailable", "Chưa xác minh lĩnh vực.");
        var officer = officers.SingleOrDefault(o => o.Id == input.SpecialistId);
        if (officer == null || (await SpecialistReasons(app, officer, category.Value, ct)).Length > 0)
            return Fail<ApplicationConsultation>(422, "specialist_ineligible", "Cán bộ chưa đủ điều kiện tham vấn.");
        if (await db.Consultations.AnyAsync(c => c.ApplicationId == applicationId && c.SpecialistId == officer.Id && c.Status != "CLOSED", ct))
            return Fail<ApplicationConsultation>(409, "already_open", "Đã có tham vấn cho cán bộ này.");
        app.CategoryId = category;
        item.SpecialistId = officer.Id;
        item.SpecialistName = officer.FullName;
        item.DispatchedBy = actor.Id;
        item.DispatchedAtUtc = clock.GetUtcNow().UtcDateTime;
        item.Status = "OPEN";
        WorkflowOutboxCapture.Staffing(db, app.WardCode!, item.Id, officer.Id, "CONSULTATION_DISPATCHED", false, item.DispatchedAtUtc.Value);
        item.Version++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return WorkflowResult<ApplicationConsultation>.Ok(item);
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
            (actor.IsManager || app.AssignedOfficerId == actor.Id || x.SpecialistId == actor.Id || x.RequestedBy == actor.Id)).OrderBy(x => x.CreatedAtUtc).ToArrayAsync(ct));
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
            ApplicationCode = app.ApplicationCode, SpecialistName = officer.FullName, RequestedByName = actor.FullName,
            AssignmentVersion = app.AssignmentVersion, Question = input.Question.Trim(), CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        db.Consultations.Add(item);
        WorkflowOutboxCapture.Staffing(db, app.WardCode!, item.Id, officer.Id, "CONSULTATION_DISPATCHED", false, item.CreatedAtUtc);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return WorkflowResult<ApplicationConsultation>.Ok(item);
    }
    public async Task<WorkflowResult<ApplicationConsultation>> Reply(ReviewActor actor, Guid applicationId, Guid id, ConsultationReply input, bool close, CancellationToken ct, string token = "")
    {
        if (!Text(input.Text)) return Fail<ApplicationConsultation>(400, "invalid_text", "Cần nội dung tối đa 4000 ký tự.");
        if (input.Decision is not (null or "DECLINE_REQUEST" or "KEEP_ASSIGNEE" or "TRANSFER_TO_SPECIALIST") || !close && input.Decision != null)
            return Fail<ApplicationConsultation>(400, "invalid_decision", "Quyết định không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (close && input.Decision == "TRANSFER_TO_SPECIALIST" && actor.WardCode != null)
            await StaffingLocks.Ward(db, actor.WardCode, ct);
        var app = await LockedApplication(applicationId, ct);
        if (app is null || !Scope(actor, app)) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        var item = await db.Consultations.SingleOrDefaultAsync(x => x.Id == id && x.ApplicationId == applicationId, ct);
        var originalAssignee = app.AssignedOfficerId;
        if (item is null) return Fail<ApplicationConsultation>(404, "not_found", "Không tìm thấy tham vấn.");
        if (close ? !actor.IsManager : !actor.CanMutate || item.SpecialistId != actor.Id) return Fail<ApplicationConsultation>(403, "forbidden", "Không có quyền thao tác tham vấn này.");
        if (item.Version != input.ExpectedVersion || item.Status == "CLOSED" || !close && item.Status != "OPEN") return Fail<ApplicationConsultation>(409, "version_conflict", "Tham vấn đã thay đổi. Tải lại dữ liệu.");
        if ((!close || input.Decision == "TRANSFER_TO_SPECIALIST") && (app.Status != "UNDER_REVIEW" || app.AssignmentVersion != item.AssignmentVersion ||
            await db.Versions.Where(x => x.ApplicationId == applicationId).MaxAsync(x => (int?)x.VersionNumber, ct) != item.ApplicationVersion))
            return Fail<ApplicationConsultation>(409, "application_changed", "Hồ sơ hoặc phân công đã thay đổi; Manager cần tạo tham vấn mới.");
        if (!close && app.CategoryId is int category && actor.AssignedCategories?.Contains(category) != true)
            return Fail<ApplicationConsultation>(403, "category_mismatch", "Cán bộ không còn phụ trách lĩnh vực này.");
        if (close && !item.AllowsDecision(input.Decision))
            return Fail<ApplicationConsultation>(409, "invalid_status", "Quyết định chưa phù hợp trạng thái tham vấn.");
        if (close && input.Decision == "TRANSFER_TO_SPECIALIST")
        {
            if (item.DispatchedBy.HasValue && !item.WillingToTakeOver) return Fail<ApplicationConsultation>(422, "takeover_not_accepted", "Cán bộ mới chỉ đồng ý góp ý.");
            if (input.ExpectedAssignmentVersion != app.AssignmentVersion)
                return Fail<ApplicationConsultation>(409, "version_conflict", "Tải lại phiên bản phân công.");
            var categoryId = await Category(app, ct);
            if (categoryId is not > 0) return Fail<ApplicationConsultation>(503, "category_unavailable", "Chưa xác minh lĩnh vực.");
            var specialist = (await directory.Officers(token, ct)).SingleOrDefault(o => o.Id == item.SpecialistId);
            if (specialist == null || app.AssignedOfficerId == specialist.Id || DispatchService.Reasons(app, specialist, categoryId.Value).Length != 0 ||
                (await StaffingLocks.Availability(db, specialist.Id, app.WardCode!, clock.GetUtcNow().UtcDateTime, ct)).Length != 0)
                return Fail<ApplicationConsultation>(422, "officer_ineligible", "Cán bộ chưa đủ điều kiện nhận hồ sơ.");
            DispatchService.RecordAssignment(db, app, "TRANSFER", actor, specialist, input.Text, clock.GetUtcNow().UtcDateTime);
        }
        if (close) { item.Decision = input.Decision; item.Status = "CLOSED"; item.CloseReason = input.Text.Trim(); item.ClosedAtUtc = clock.GetUtcNow().UtcDateTime; item.ClosedBy = actor.Id; item.ClosedByName = actor.FullName; }
        else { item.Status = "ANSWERED"; item.WillingToTakeOver = input.WillingToTakeOver; item.Answer = input.Text.Trim(); item.AnsweredAtUtc = clock.GetUtcNow().UtcDateTime; }
        var requester = item.DispatchedBy != null || item.Decision == "DECLINE_REQUEST" ? item.RequestedBy : originalAssignee;
        if (requester is Guid recipient)
            WorkflowOutboxCapture.Staffing(db, app.WardCode!, item.Id, recipient, close ? "CONSULTATION_CLOSED" : "CONSULTATION_ANSWERED", false, clock.GetUtcNow().UtcDateTime);
        if (!close)
            WorkflowOutboxCapture.Staffing(db, app.WardCode!, item.Id, actor.Id, "CONSULTATION_ANSWERED", true, clock.GetUtcNow().UtcDateTime);
        item.Version++; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return WorkflowResult<ApplicationConsultation>.Ok(item);
    }
}
