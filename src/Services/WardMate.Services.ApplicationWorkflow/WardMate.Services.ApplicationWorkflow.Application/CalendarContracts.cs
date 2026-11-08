using WardMate.Services.ApplicationWorkflow.Domain;
namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record CalendarActor(Guid Id, string? WardCode, bool CanReadWard, bool CanManageWard, bool CanManageNational, string? FullName = null);
public sealed record ClosureInput(DateOnly Date, string Reason);
public sealed record ClosureCancelInput(string Reason);
public sealed record ClosureDto(Guid Id, string? WardCode, DateOnly Date, DateTime EffectiveFromUtc, string Reason,
    Guid CreatedBy, DateTime CreatedAtUtc, bool Cancelled, Guid? CancelledBy, DateTime? CancelledAtUtc, string? CancellationReason,
    string? WardName = null, string? CreatedByName = null, string? CancelledByName = null)
{
    public static ClosureDto From(CalendarClosure x) => new(x.Id,x.WardCode,x.ClosureDate,x.EffectiveFromUtc,x.Reason,
        x.CreatedBy,x.CreatedAtUtc,x.CancelledAtUtc != null,x.CancelledBy,x.CancelledAtUtc,x.CancellationReason,
        x.WardName,x.CreatedByName,x.CancelledByName);
}
public sealed record ClosureChange(ClosureDto Closure, int RescheduledApplications);
public interface ICalendarClosureService
{
    Task<WorkflowResult<ClosureDto[]>> List(CalendarActor actor, DateOnly from, DateOnly to, CancellationToken ct);
    Task<WorkflowResult<ClosureChange>> Create(CalendarActor actor, ClosureInput input, bool national, CancellationToken ct);
    Task<WorkflowResult<ClosureChange>> Cancel(CalendarActor actor, Guid id, ClosureCancelInput input, CancellationToken ct);
}
