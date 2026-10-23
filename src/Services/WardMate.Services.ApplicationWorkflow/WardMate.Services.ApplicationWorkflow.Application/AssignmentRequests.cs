using MediatR;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ReleaseApplicationInput(string Reason, long? ExpectedAssignmentVersion = null);
public sealed record ManageAssigneeInput(Guid OfficerId, string Reason, long? ExpectedAssignmentVersion);
public sealed record OfficerTarget(Guid Id, string? FullName, string? WardCode);
public sealed record ReleaseApplicationCommand(ReviewActor Actor, Guid Id, ReleaseApplicationInput Input) : IRequest<WorkflowResult<ReviewResultDto>>;
public sealed record ManageAssigneeCommand(ReviewActor Actor, Guid Id, ManageAssigneeInput Input, OfficerTarget Target) : IRequest<WorkflowResult<ReviewResultDto>>;

public sealed class AssignmentHandlers(IApplicationReviewStore store, TimeProvider clock) :
    IRequestHandler<ReleaseApplicationCommand, WorkflowResult<ReviewResultDto>>,
    IRequestHandler<ManageAssigneeCommand, WorkflowResult<ReviewResultDto>>
{
    private static WorkflowResult<ReviewResultDto> Fail(int status, string code, string text) => WorkflowResult<ReviewResultDto>.Fail(status, "workflow." + code, text);
    public static bool ValidReason(string? reason) => reason?.Trim().Length is >= 5 and <= 500;
    public static void Record(ApplicationRecord a, string action, Guid actor, Guid? target, string? name, string? reason, DateTime now)
    {
        a.AssignmentHistory.Add(new() { ApplicationId = a.Id, Action = action, ActorId = actor,
            FromOfficerId = a.AssignedOfficerId, ToOfficerId = target, Reason = reason, CreatedAt = now });
        a.AssignedOfficerId = target; a.AssignedOfficerName = name; a.AssignedAt = target is null ? null : now;
        a.AssignmentVersion++; a.UpdatedAt = now;
    }
    private static WorkflowResult<ReviewResultDto> Ok(ApplicationRecord a) => WorkflowResult<ReviewResultDto>.Ok(new(ApplicationDto.From(a), a.AssignedOfficerId, a.ResubmitCount, a.Versions.Select(x => x.VersionNumber).DefaultIfEmpty().Max()));
    public Task<WorkflowResult<ReviewResultDto>> Handle(ReleaseApplicationCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        if (!ValidReason(r.Input.Reason)) return Fail(400, "validation", "Lý do phải có 5–500 ký tự.");
        if (!r.Actor.CanMutate || string.IsNullOrWhiteSpace(r.Actor.WardCode) || a.WardCode != r.Actor.WardCode || a.UserId == r.Actor.Id)
            return Fail(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        if (a.AssignedOfficerId != r.Actor.Id) return Fail(403, "not_assignee", "Bạn không phải cán bộ đang phụ trách.");
        if (r.Input.ExpectedAssignmentVersion is { } version && version != a.AssignmentVersion) return Fail(409, "assignment_conflict", "Phân công đã thay đổi. Hãy tải lại hồ sơ.");
        if (a.Status != ApplicationStates.UnderReview) return Fail(409, "invalid_status", "Chỉ trả lại hồ sơ UNDER_REVIEW.");
        var now = clock.GetUtcNow().UtcDateTime;
        a.History.Add(new() { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = ApplicationStates.Submitted, ChangedBy = r.Actor.Id, CreatedAt = now });
        a.Status = ApplicationStates.Submitted;
        Record(a, "RELEASE", r.Actor.Id, null, null, r.Input.Reason.Trim(), now);
        return Ok(a);
    }, ct);
    public Task<WorkflowResult<ReviewResultDto>> Handle(ManageAssigneeCommand r, CancellationToken ct) => store.Mutate(r.Id, a =>
    {
        if (!ValidReason(r.Input.Reason) || r.Input.ExpectedAssignmentVersion is null or < 0 || r.Input.OfficerId == Guid.Empty)
            return Fail(400, "validation", "Cần lý do 5–500 ký tự, officerId và expectedAssignmentVersion hợp lệ.");
        if (!r.Actor.IsManager || string.IsNullOrWhiteSpace(r.Actor.WardCode) || a.WardCode != r.Actor.WardCode || a.UserId == r.Actor.Id)
            return Fail(404, "not_found", "Không tìm thấy hồ sơ trong phạm vi.");
        if (r.Target.Id != r.Input.OfficerId || r.Target.WardCode != a.WardCode || r.Target.Id == a.UserId)
            return Fail(422, "invalid_assignee", "Cán bộ đích không hợp lệ.");
        if (r.Input.ExpectedAssignmentVersion != a.AssignmentVersion) return Fail(409, "assignment_conflict", "Phân công đã thay đổi. Hãy tải lại hồ sơ.");
        if (a.AssignedOfficerId == r.Target.Id) return Fail(409, "same_assignee", "Cán bộ này đang phụ trách hồ sơ.");
        var initial = a.Status == ApplicationStates.Submitted && a.AssignedOfficerId is null;
        if (!initial && (a.Status is not (ApplicationStates.UnderReview or ApplicationStates.NeedRevision or ApplicationStates.DocumentsReady) || a.AssignedOfficerId is null))
            return Fail(409, "invalid_status", "Trạng thái không cho phép giao/chuyển hồ sơ.");
        if (a.Versions.Count == 0) return Fail(409, "snapshot_missing", "Hồ sơ chưa có snapshot nộp.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (initial)
        {
            a.History.Add(new() { ApplicationId = a.Id, FromStatus = a.Status, ToStatus = ApplicationStates.UnderReview, ChangedBy = r.Actor.Id, CreatedAt = now });
            a.Status = ApplicationStates.UnderReview;
        }
        Record(a, initial ? "ASSIGN" : "TRANSFER", r.Actor.Id, r.Target.Id, r.Target.FullName, r.Input.Reason.Trim(), now);
        return Ok(a);
    }, ct);
}
