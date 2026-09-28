using MediatR;
using WardMate.Services.IAM.Application.Common;

namespace WardMate.Services.IAM.Application.Rbac;

public sealed record ListRolesQuery(int Page = 1, int PageSize = 20) : IRequest<Result<RbacPage<RoleDto>>>;
public sealed record GetRoleQuery(int RoleId) : IRequest<Result<RoleDto>>;
public sealed record ListPermissionsQuery : IRequest<Result<PermissionDto[]>>;
public sealed record GetUserRolesQuery(Guid UserId) : IRequest<Result<RoleDto[]>>;
public sealed record ListRbacAuditQuery(int Page = 1, int PageSize = 20) : IRequest<Result<RbacPage<AuditDto>>>;
public sealed record CreateRoleCommand(Guid ActorId, RoleInput Input) : IRequest<Result<RoleDto>>;
public sealed record UpdateRoleCommand(Guid ActorId, int RoleId, RoleInput Input) : IRequest<Result<RoleDto>>;
public sealed record DeleteRoleCommand(Guid ActorId, int RoleId) : IRequest<Result<bool>>;
public sealed record SetRolePermissionCommand(Guid ActorId, int RoleId, int PermissionId, bool Grant) : IRequest<Result<bool>>;
public sealed record SetUserRoleCommand(Guid ActorId, Guid UserId, int RoleId, bool Grant) : IRequest<Result<bool>>;
