using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

// The same ward lock is used by scheduling, leave decisions and assignment writes.
internal static class StaffingLocks
{
    public static Task Ward(WorkflowDbContext db, string ward, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({ward}, 301))", ct);
    public static async Task<string[]> Availability(WorkflowDbContext db, Guid officer, string? ward, DateTime now, CancellationToken ct)
    {
        var reasons = new List<string>();
        if (!await db.Shifts.AnyAsync(x => x.OfficerId == officer && x.WardCode == ward && x.CancelledAtUtc == null && x.StartsAtUtc <= now && x.EndsAtUtc > now, ct))
            reasons.Add("NO_ACTIVE_SHIFT");
        if (await db.Leaves.AnyAsync(x => x.OfficerId == officer && x.WardCode == ward && x.Status == "APPROVED" && x.StartsAtUtc <= now && x.EndsAtUtc > now, ct))
            reasons.Add("APPROVED_LEAVE");
        return reasons.ToArray();
    }
}

public sealed class StaffingService(WorkflowDbContext db, IDispatchDirectory directory, TimeProvider clock) : IStaffingService
{
    private static bool Manager(ReviewActor a) => a.IsManager && !string.IsNullOrWhiteSpace(a.WardCode);
    private static bool Officer(ReviewActor a) => a.CanMutate && !string.IsNullOrWhiteSpace(a.WardCode);
    private static WorkflowResult<T> Fail<T>(int status, string code, string text) => WorkflowResult<T>.Fail(status, "staffing." + code, text);
    private static bool Range(DateTimeOffset from, DateTimeOffset to) => to > from && to - from <= TimeSpan.FromDays(93);
    private static bool Reason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Length <= 2000;
    public async Task<WorkflowResult<OfficerShift[]>> Shifts(ReviewActor a, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (!Manager(a) && !Officer(a)) return Fail<OfficerShift[]>(403, "forbidden", "Không có quyền xem lịch.");
        if (!Range(from, to)) return Fail<OfficerShift[]>(400, "invalid_range", "Khoảng thời gian phải tăng dần, tối đa 93 ngày.");
        return WorkflowResult<OfficerShift[]>.Ok(await db.Shifts.AsNoTracking().Where(x => x.WardCode == a.WardCode && (a.IsManager || x.OfficerId == a.Id) && x.StartsAtUtc < to.UtcDateTime && x.EndsAtUtc > from.UtcDateTime).OrderBy(x => x.StartsAtUtc).ToArrayAsync(ct));
    }
    public async Task<WorkflowResult<OfficerShift>> CreateShift(ReviewActor a, ShiftInput input, string token, CancellationToken ct)
    {
        if (!Manager(a)) return Fail<OfficerShift>(403, "forbidden", "Chỉ Manager quản lý lịch.");
        if (!Range(input.StartsAt, input.EndsAt) || input.EndsAt - input.StartsAt > TimeSpan.FromHours(24) || input.StartsAt.UtcDateTime < clock.GetUtcNow().UtcDateTime)
            return Fail<OfficerShift>(400, "invalid_range", "Ca làm phải bắt đầu trong tương lai, dài tối đa 24 giờ.");
        var target = (await directory.Officers(token, ct)).SingleOrDefault(x => x.Id == input.OfficerId);
        if (target is null || !target.IsActive || target.WardCode != a.WardCode || !target.Roles.Contains("FRONT_DESK_OFFICER") || target.Roles.Contains("MANAGER"))
            return Fail<OfficerShift>(422, "officer_ineligible", "Cần cán bộ hoạt động thuộc phường quản lý.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StaffingLocks.Ward(db, a.WardCode!, ct);
        var start = input.StartsAt.UtcDateTime; var end = input.EndsAt.UtcDateTime;
        if (await db.Shifts.AnyAsync(x => x.WardCode == a.WardCode && x.OfficerId == target.Id && x.CancelledAtUtc == null && x.StartsAtUtc < end && x.EndsAtUtc > start, ct) ||
            await db.Leaves.AnyAsync(x => x.WardCode == a.WardCode && x.OfficerId == target.Id && x.Status == "APPROVED" && x.StartsAtUtc < end && x.EndsAtUtc > start, ct))
            return Fail<OfficerShift>(409, "overlap", "Ca làm trùng lịch hoặc nghỉ đã duyệt.");
        var item = new OfficerShift { OfficerId = target.Id, WardCode = a.WardCode!, StartsAtUtc = start, EndsAtUtc = end, CreatedBy = a.Id, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        db.Shifts.Add(item); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return WorkflowResult<OfficerShift>.Ok(item);
    }
    public async Task<WorkflowResult<OfficerShift>> CancelShift(ReviewActor a, Guid id, StaffingReason input, CancellationToken ct)
    {
        if (!Manager(a)) return Fail<OfficerShift>(403, "forbidden", "Chỉ Manager quản lý lịch.");
        if (!Reason(input.Reason)) return Fail<OfficerShift>(400, "reason_required", "Cần lý do tối đa 2000 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await StaffingLocks.Ward(db, a.WardCode!, ct);
        var item = await db.Shifts.SingleOrDefaultAsync(x => x.Id == id && x.WardCode == a.WardCode, ct);
        if (item is null) return Fail<OfficerShift>(404, "not_found", "Không tìm thấy ca làm trong phường.");
        if (item.CancelledAtUtc != null || item.EndsAtUtc <= clock.GetUtcNow().UtcDateTime) return Fail<OfficerShift>(409, "invalid_status", "Ca đã hủy hoặc đã kết thúc.");
        item.CancelledBy = a.Id; item.CancelledAtUtc = clock.GetUtcNow().UtcDateTime; item.CancellationReason = input.Reason.Trim();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return WorkflowResult<OfficerShift>.Ok(item);
    }
    public async Task<WorkflowResult<OfficerLeave[]>> Leaves(ReviewActor a, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (!Manager(a) && !Officer(a)) return Fail<OfficerLeave[]>(403, "forbidden", "Không có quyền xem nghỉ phép.");
        if (!Range(from, to)) return Fail<OfficerLeave[]>(400, "invalid_range", "Khoảng thời gian phải tăng dần, tối đa 93 ngày.");
        return WorkflowResult<OfficerLeave[]>.Ok(await db.Leaves.AsNoTracking().Where(x => x.WardCode == a.WardCode && (a.IsManager || x.OfficerId == a.Id) && x.StartsAtUtc < to.UtcDateTime && x.EndsAtUtc > from.UtcDateTime).OrderBy(x => x.StartsAtUtc).ToArrayAsync(ct));
    }
    public async Task<WorkflowResult<OfficerLeave>> RequestLeave(ReviewActor a, LeaveInput input, CancellationToken ct)
    {
        if (!Officer(a)) return Fail<OfficerLeave>(403, "forbidden", "Chỉ cán bộ gửi yêu cầu nghỉ của mình.");
        if (!Range(input.StartsAt, input.EndsAt) || input.EndsAt.UtcDateTime <= clock.GetUtcNow().UtcDateTime || !Reason(input.Reason))
            return Fail<OfficerLeave>(400, "invalid_input", "Cần khoảng nghỉ chưa kết thúc, tối đa 93 ngày và lý do tối đa 2000 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await StaffingLocks.Ward(db, a.WardCode!, ct);
        var start = input.StartsAt.UtcDateTime; var end = input.EndsAt.UtcDateTime;
        if (await db.Leaves.AnyAsync(x => x.OfficerId == a.Id && x.WardCode == a.WardCode && (x.Status == "PENDING" || x.Status == "APPROVED") && x.StartsAtUtc < end && x.EndsAtUtc > start, ct))
            return Fail<OfficerLeave>(409, "overlap", "Đã có yêu cầu nghỉ đang chờ hoặc đã duyệt trùng thời gian.");
        var item = new OfficerLeave { OfficerId = a.Id, WardCode = a.WardCode!, StartsAtUtc = start, EndsAtUtc = end, Reason = input.Reason.Trim(), CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        db.Leaves.Add(item); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return WorkflowResult<OfficerLeave>.Ok(item);
    }
    public async Task<WorkflowResult<OfficerLeave>> DecideLeave(ReviewActor a, Guid id, LeaveDecisionInput input, CancellationToken ct)
    {
        if (!Manager(a) && !Officer(a)) return Fail<OfficerLeave>(403, "forbidden", "Không có quyền thao tác nghỉ phép.");
        if (input.Decision is not ("APPROVED" or "REJECTED" or "CANCELLED") || !Reason(input.Reason)) return Fail<OfficerLeave>(400, "invalid_input", "Quyết định hoặc lý do không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await StaffingLocks.Ward(db, a.WardCode!, ct);
        var item = await db.Leaves.SingleOrDefaultAsync(x => x.Id == id && x.WardCode == a.WardCode && (a.IsManager || x.OfficerId == a.Id), ct);
        if (item is null) return Fail<OfficerLeave>(404, "not_found", "Không tìm thấy yêu cầu nghỉ.");
        if (a.Id == item.OfficerId && input.Decision != "CANCELLED" || !a.IsManager && input.Decision != "CANCELLED") return Fail<OfficerLeave>(403, "forbidden", "Không được tự duyệt nghỉ.");
        if (item.Version != input.ExpectedVersion || item.EndsAtUtc <= clock.GetUtcNow().UtcDateTime ||
            (item.Status != "PENDING" && !(a.IsManager && item.Status == "APPROVED" && input.Decision == "CANCELLED")))
            return Fail<OfficerLeave>(409, "version_or_status", "Yêu cầu đã thay đổi, hết hạn hoặc không cho phép quyết định này.");
        db.LeaveDecisions.Add(new OfficerLeaveDecision { LeaveId = item.Id, ActorId = a.Id, Version = item.Version + 1, FromStatus = item.Status,
            ToStatus = input.Decision, Reason = input.Reason.Trim(), CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
        item.Status = input.Decision; item.Version++; item.DecidedBy = a.Id; item.DecidedAtUtc = clock.GetUtcNow().UtcDateTime; item.DecisionReason = input.Reason.Trim();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return WorkflowResult<OfficerLeave>.Ok(item);
    }
    public async Task<WorkflowResult<DispatchApplication[]>> Impact(ReviewActor a, Guid id, CancellationToken ct)
    {
        if (!Manager(a)) return Fail<DispatchApplication[]>(403, "forbidden", "Chỉ Manager xem hồ sơ cần bàn giao.");
        var leave = await db.Leaves.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.WardCode == a.WardCode, ct);
        if (leave is null) return Fail<DispatchApplication[]>(404, "not_found", "Không tìm thấy yêu cầu nghỉ.");
        return WorkflowResult<DispatchApplication[]>.Ok(await db.Applications.AsNoTracking().Where(x => x.WardCode == a.WardCode && x.UserId != a.Id && x.AssignedOfficerId == leave.OfficerId && (x.Status == "SUBMITTED" || x.Status == "UNDER_REVIEW" || x.Status == "NEED_REVISION" || x.Status == "DOCUMENTS_READY"))
            .OrderBy(x => x.DueAtUtc).Select(x => new DispatchApplication(x.Id, x.ApplicationCode, x.ProcedureId, x.ProcedureTitle, x.Status, x.AssignedOfficerId, x.AssignmentVersion, x.CategoryId)).ToArrayAsync(ct));
    }
    public async Task<WorkflowResult<OfficerLeaveDecision[]>> LeaveHistory(ReviewActor a, Guid id, CancellationToken ct)
    {
        if (!Manager(a) && !Officer(a)) return Fail<OfficerLeaveDecision[]>(403, "forbidden", "Không có quyền xem lịch sử nghỉ.");
        if (!await db.Leaves.AnyAsync(x => x.Id == id && x.WardCode == a.WardCode && (a.IsManager || x.OfficerId == a.Id), ct))
            return Fail<OfficerLeaveDecision[]>(404, "not_found", "Không tìm thấy yêu cầu nghỉ trong phạm vi.");
        return WorkflowResult<OfficerLeaveDecision[]>.Ok(await db.LeaveDecisions.AsNoTracking().Where(x => x.LeaveId == id).OrderBy(x => x.Version).ToArrayAsync(ct));
    }
}
