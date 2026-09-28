using MediatR;
using WardMate.Services.IAM.Application.Common;

namespace WardMate.Services.IAM.Application.Rbac;

public sealed class RbacQueryHandlers(IRbacStore store) :
    IRequestHandler<ListRolesQuery, Result<RbacPage<RoleDto>>>, IRequestHandler<GetRoleQuery, Result<RoleDto>>,
    IRequestHandler<ListPermissionsQuery, Result<PermissionDto[]>>, IRequestHandler<GetUserRolesQuery, Result<RoleDto[]>>,
    IRequestHandler<ListRbacAuditQuery, Result<RbacPage<AuditDto>>>
{
    public async Task<Result<RbacPage<RoleDto>>> Handle(ListRolesQuery request, CancellationToken ct) =>
        Result<RbacPage<RoleDto>>.Success(await store.ListRoles(request.Page, request.PageSize, ct));
    public async Task<Result<RoleDto>> Handle(GetRoleQuery request, CancellationToken ct)
    {
        var role = await store.FindRole(request.RoleId, ct);
        return role is null ? Result<RoleDto>.Failure(RbacErrors.RoleNotFound) : Result<RoleDto>.Success(RoleDto.From(role));
    }
    public async Task<Result<PermissionDto[]>> Handle(ListPermissionsQuery request, CancellationToken ct) =>
        Result<PermissionDto[]>.Success(await store.ListPermissions(ct));
    public async Task<Result<RoleDto[]>> Handle(GetUserRolesQuery request, CancellationToken ct) =>
        await store.UserExists(request.UserId, ct) ? Result<RoleDto[]>.Success(await store.UserRoles(request.UserId, ct))
            : Result<RoleDto[]>.Failure(RbacErrors.UserNotFound);
    public async Task<Result<RbacPage<AuditDto>>> Handle(ListRbacAuditQuery request, CancellationToken ct) =>
        Result<RbacPage<AuditDto>>.Success(await store.ListAudit(request.Page, request.PageSize, ct));
}
