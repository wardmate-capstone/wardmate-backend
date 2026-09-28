using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Rbac;

public sealed record RoleInput(string RoleName, string? Description);
public sealed record PermissionDto(int Id, string PermissionCode, string PermissionName, string Module);
public sealed record RoleDto(int Id, string RoleName, string? Description, bool IsSystem, PermissionDto[] Permissions)
{
    public static RoleDto From(Role role) => new(role.Id, role.RoleName, role.Description, RbacRules.IsSystem(role.RoleName),
        role.RolePermissions.OrderBy(x => x.PermissionId).Select(x => new PermissionDto(x.Permission.Id,
            x.Permission.PermissionCode, x.Permission.PermissionName, x.Permission.Module)).ToArray());
}
public sealed record RbacPage<T>(T[] Items, int Page, int PageSize, int Total);
public sealed record AuditDto(Guid Id, Guid ActorUserId, string Action, Guid? TargetUserId, int? RoleId,
    int? PermissionId, string Details, DateTime CreatedAt);

public static class RbacRules
{
    public static bool IsSystem(string name) => name is RoleNames.RegisteredCitizen or RoleNames.FrontDeskOfficer
        or RoleNames.Manager or RoleNames.ProcedureManager or RoleNames.ItAdmin;
}

public static class RbacErrors
{
    public static readonly Error Forbidden = new("iam.forbidden", "Bạn không có quyền quản trị phân quyền.", ErrorKind.Forbidden);
    public static readonly Error RoleNotFound = new("iam.role_not_found", "Không tìm thấy vai trò.", ErrorKind.NotFound);
    public static readonly Error PermissionNotFound = new("iam.permission_not_found", "Không tìm thấy quyền.", ErrorKind.NotFound);
    public static readonly Error UserNotFound = new("iam.user_not_found", "Không tìm thấy tài khoản.", ErrorKind.NotFound);
    public static readonly Error SystemRole = new("iam.system_role_protected", "Không thể đổi tên hoặc xóa vai trò hệ thống.", ErrorKind.Conflict);
    public static readonly Error AssignedRole = new("iam.role_in_use", "Vai trò đang được gán cho người dùng. Hãy thu hồi trước khi xóa.", ErrorKind.Conflict);
    public static readonly Error LastAdmin = new("iam.last_admin", "Phải duy trì ít nhất một IT_ADMIN đang hoạt động.", ErrorKind.Conflict);
    public static readonly Error AdminPermission = new("iam.admin_permission_protected", "Không thể thu hồi quyền quản trị hệ thống của IT_ADMIN.", ErrorKind.Conflict);
    public static readonly Error Duplicate = new("iam.rbac_conflict", "Tên vai trò hoặc liên kết phân quyền đã tồn tại.", ErrorKind.Conflict);
}
