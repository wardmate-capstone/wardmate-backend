namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ScheduleSlot(Guid ShiftId, Guid OfficerId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int ShiftVersion = 0);
public sealed record WeekEntry(DayOfWeek Day, TimeOnly Start, TimeOnly End, Guid OfficerId);
public sealed record WeekEdit(int ExpectedVersion, WeekEntry[] Entries);
public sealed record ScheduleDraftInput(int ExpectedVersion, string BaseFingerprint, ScheduleSlot[] Slots, string Reason);
public sealed record PublishSchedule(Guid RevisionId, int ExpectedVersion, bool AllowClosure, string Reason);
public sealed record ScheduleConfirmation(string Fingerprint);
public sealed record ChangeShiftInput(Guid ShiftId, int ExpectedVersion, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason);
public sealed record DecideShift(string Decision, string Reason);
public sealed record MonthlyRevisionView(Guid Id,DateOnly Month,string Status,int Version,string BaseFingerprint,Guid ActorId,string Reason,DateTime? PublishedAtUtc,ScheduleSlot[] Slots);
public sealed record ScheduleLeaveView(Guid Id,Guid OfficerId,DateTime StartsAtUtc,DateTime EndsAtUtc);
public sealed record ScheduleClosureView(Guid Id,DateOnly ClosureDate,string? WardCode);
public sealed record ScheduleReceiptView(Guid OfficerId,DateTime? ConfirmedAtUtc);
public sealed record MonthlyView(DateOnly Month,ScheduleSlot[] Slots,string BaseFingerprint,MonthlyRevisionView? Draft,
    ScheduleLeaveView[] Leaves,ScheduleClosureView[] Closures,ScheduleReceiptView[] Confirmations);
public sealed record WeekView(int Version,WeekEntry[] Entries);
public sealed record PreviewView(DateOnly Month,ScheduleSlot[] Slots,DateOnly[] Closures,List<string> Conflicts,string BaseFingerprint);
public sealed record ChangePage(WardMate.Services.ApplicationWorkflow.Domain.ShiftChangeRequest[] Items,int Page,int PageSize,int Total);
public interface IMonthlySchedules
{
    Task<WorkflowResult<object>> Month(ReviewActor actor, DateOnly month, CancellationToken ct);
    Task<WorkflowResult<object>> Week(ReviewActor actor, WeekEdit? edit, CancellationToken ct);
    Task<WorkflowResult<object>> Preview(ReviewActor actor, DateOnly month, DateOnly? copyFrom, CancellationToken ct);
    Task<WorkflowResult<object>> Draft(ReviewActor actor, DateOnly month, ScheduleDraftInput input, CancellationToken ct);
    Task<WorkflowResult<object>> Publish(ReviewActor actor, DateOnly month, PublishSchedule input, string token, CancellationToken ct);
    Task<WorkflowResult<object>> History(ReviewActor actor, DateOnly month, CancellationToken ct);
    Task<WorkflowResult<object>> Confirm(ReviewActor actor, DateOnly month, ScheduleConfirmation input, CancellationToken ct);
    Task<WorkflowResult<object>> Changes(ReviewActor actor, int page, int pageSize, CancellationToken ct);
    Task<WorkflowResult<object>> RequestChange(ReviewActor actor, ChangeShiftInput input, CancellationToken ct);
    Task<WorkflowResult<object>> DecideChange(ReviewActor actor, Guid id, DecideShift input,string token,CancellationToken ct);
}
