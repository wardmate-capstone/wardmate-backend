using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record DispatchAssignmentInput(Guid OfficerId, long ExpectedAssignmentVersion, string ReasonCode, string Reason);
public sealed record DispatchOfficer(Guid Id, string? FullName, bool IsActive, string? WardCode, string[] Roles, int[] AssignedCategories, string[]? Permissions = null);
public sealed record DispatchOfficerPage(DispatchOfficer[] Items, int Total);
public sealed record DispatchCandidate(Guid OfficerId, string? FullName, bool Eligible, int ActiveApplicationCount, string[] BlockingReasons);
public sealed record DispatchApplication(Guid Id, string? ApplicationCode, Guid ProcedureId, string ProcedureTitle, string Status,
    Guid? AssignedOfficerId, long AssignmentVersion, int? ProcedureCategoryId);
public interface IDispatchDirectory
{
    Task<DispatchOfficer[]> Officers(string token, CancellationToken ct);
}
public interface IDispatchService
{
    Task<WorkflowResult<object>> List(ReviewActor actor, int page, int pageSize, CancellationToken ct);
    Task<WorkflowResult<DispatchCandidate[]>> Candidates(ReviewActor actor, Guid id, string token, CancellationToken ct);
    Task<WorkflowResult<AssignmentRecord>> Assign(ReviewActor actor, Guid id, DispatchAssignmentInput input, string token, CancellationToken ct);
    Task<WorkflowResult<AssignmentRecord[]>> History(ReviewActor actor, Guid id, CancellationToken ct);
}
