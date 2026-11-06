namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record LeaveDraftInput(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason, Guid TemplateVersionId);
public sealed record LeaveDraftEdit(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason, int ExpectedVersion);
public sealed record LeaveDraftSubmit(int ExpectedVersion, Guid DocumentRevisionId, int MetadataVersion);
public sealed record LeaveDraftView(Guid Id, int Version, int MetadataVersion, Guid TemplateVersionId,
    DateTime StartsAtUtc, DateTime EndsAtUtc, string Reason, Guid? DocumentRevisionId,
    Guid? SubmittedLeaveId, DateTime UpdatedAtUtc);
public sealed record LeaveSubmitReceipt(Guid LeaveId, string Status, int Version, DateTime SubmittedAtUtc);
public sealed record LeaveTemplateDescriptor(Guid VersionId, string Sha256);
public sealed record LeavePage(LeaveDraftView[] Items, int Page, int PageSize, int Total);
public interface ILeaveDrafts
{
    WorkflowResult<LeaveTemplateDescriptor> Template(ReviewActor actor);
    Task<WorkflowResult<byte[]>> TemplateFile(ReviewActor actor, Guid id, CancellationToken ct);
    Task<WorkflowResult<LeaveDraftView>> Create(ReviewActor actor, LeaveDraftInput input, Guid key, CancellationToken ct);
    Task<WorkflowResult<LeavePage>> List(ReviewActor actor, int page, int pageSize, CancellationToken ct);
    Task<WorkflowResult<LeaveDraftView>> Get(ReviewActor actor, Guid id, CancellationToken ct);
    Task<WorkflowResult<LeaveDraftView>> Edit(ReviewActor actor, Guid id, LeaveDraftEdit input, CancellationToken ct);
    Task<WorkflowResult<LeaveDraftView>> Delete(ReviewActor actor, Guid id, int version, CancellationToken ct);
    Task<WorkflowResult<byte[]>> Read(ReviewActor actor, Guid id, bool submitted, CancellationToken ct);
    Task<WorkflowResult<LeaveDraftView>> Upload(ReviewActor actor, Guid id, int version, int metadataVersion, Stream data, string filename, CancellationToken ct);
    Task<WorkflowResult<LeaveSubmitReceipt>> Submit(ReviewActor actor, Guid id, LeaveDraftSubmit input, Guid key, CancellationToken ct);
}
