namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record WorkflowAccess(Guid UserId, string? WardCode, string[] Roles, string[] Permissions, int[]? AssignedCategories = null, string? FullName = null);
public interface IWorkflowDirectory
{
    Task<WorkflowAccess?> Access(string token, CancellationToken ct);
    Task<OfficerTarget?> Officer(Guid id, string token, CancellationToken ct); Task<bool> WardExists(string code, CancellationToken ct);
}
