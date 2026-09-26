using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Rbac;

public interface IRbacStore
{
    // All RBAC writes and account status changes share one transaction lock.
    Task<Result<T>> Exclusive<T>(Func<Task<Result<T>>> operation, CancellationToken ct);
    Task<bool> IsAdministrator(Guid userId, CancellationToken ct);
    Task<bool> HasPermission(Guid userId, string permission, CancellationToken ct);
    Task<bool> IsLastActiveAdministrator(Guid userId, CancellationToken ct);
    Task<Role?> FindRole(int id, CancellationToken ct);
    Task<Permission?> FindPermission(int id, CancellationToken ct);
    Task<bool> UserExists(Guid id, CancellationToken ct);
    Task<bool> RoleIsAssigned(int id, CancellationToken ct);
    Task<UserRole?> FindAssignment(Guid userId, int roleId, CancellationToken ct);
    Task<RbacPage<RoleDto>> ListRoles(int page, int pageSize, CancellationToken ct);
    Task<PermissionDto[]> ListPermissions(CancellationToken ct);
    Task<RoleDto[]> UserRoles(Guid userId, CancellationToken ct);
    Task<RbacPage<AuditDto>> ListAudit(int page, int pageSize, CancellationToken ct);
    void AddRole(Role role);
    void DeleteRole(Role role);
    void AddAssignment(UserRole assignment);
    void DeleteAssignment(UserRole assignment);
    void DeletePermission(RolePermission permission);
    void Audit(Guid actorId, string action, Guid? userId, int? roleId, int? permissionId, string details);
    Task Save(CancellationToken ct);
}
