using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ShiftInput(Guid OfficerId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record LeaveInput(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason);
public sealed record LeaveDecisionInput(int ExpectedVersion, string Decision, string Reason);
public sealed record StaffingReason(string Reason);
public interface IStaffingService
{
    Task<WorkflowResult<OfficerShift[]>> Shifts(ReviewActor actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<WorkflowResult<OfficerShift>> CreateShift(ReviewActor actor, ShiftInput input, string token, CancellationToken ct);
    Task<WorkflowResult<OfficerShift>> CancelShift(ReviewActor actor, Guid id, StaffingReason input, CancellationToken ct);
    Task<WorkflowResult<OfficerLeave[]>> Leaves(ReviewActor actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<WorkflowResult<OfficerLeave>> RequestLeave(ReviewActor actor, LeaveInput input, CancellationToken ct);
    Task<WorkflowResult<OfficerLeave>> DecideLeave(ReviewActor actor, Guid id, LeaveDecisionInput input, CancellationToken ct);
    Task<WorkflowResult<DispatchApplication[]>> Impact(ReviewActor actor, Guid id, CancellationToken ct);
    Task<WorkflowResult<OfficerLeaveDecision[]>> LeaveHistory(ReviewActor actor, Guid id, CancellationToken ct);
}
