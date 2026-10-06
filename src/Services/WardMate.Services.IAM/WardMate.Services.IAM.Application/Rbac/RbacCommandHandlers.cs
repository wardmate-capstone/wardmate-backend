using WardMate.Services.IAM.Application.Accounts;
using System.Text.Json;
using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Rbac;

public sealed class RbacCommandHandlers(IRbacStore store, IManagementScope scope) : IRequestHandler<CreateRoleCommand, Result<RoleDto>>,
    IRequestHandler<UpdateRoleCommand, Result<RoleDto>>, IRequestHandler<DeleteRoleCommand, Result<bool>>,
    IRequestHandler<SetRolePermissionCommand, Result<bool>>, IRequestHandler<SetUserRoleCommand, Result<bool>>
{
    private Task<Result<T>> Run<T>(Guid actor, Func<Task<Result<T>>> operation, CancellationToken ct) =>
        store.Exclusive(async () => await store.IsAdministrator(actor, ct)
            ? await operation() : Result<T>.Failure(RbacErrors.Forbidden), ct);

    public Task<Result<RoleDto>> Handle(CreateRoleCommand request, CancellationToken ct) => Run(request.ActorId, async () =>
    {
        var role = new Role { RoleName = request.Input.RoleName.Trim().ToUpperInvariant(), Description = Description(request.Input.Description) };
        store.AddRole(role);
        await store.Save(ct);
        store.Audit(request.ActorId, "role.created", null, role.Id, null, JsonSerializer.Serialize(new { role.RoleName, role.Description }));
        return Result<RoleDto>.Success(RoleDto.From(role));
    }, ct);

    public Task<Result<RoleDto>> Handle(UpdateRoleCommand request, CancellationToken ct) => Run(request.ActorId, async () =>
    {
        var role = await store.FindRole(request.RoleId, ct);
        if (role is null) return Result<RoleDto>.Failure(RbacErrors.RoleNotFound);
        var name = request.Input.RoleName.Trim().ToUpperInvariant();
        if (RbacRules.IsSystem(role.RoleName) && role.RoleName != name) return Result<RoleDto>.Failure(RbacErrors.SystemRole);
        var before = new { role.RoleName, role.Description };
        role.RoleName = name;
        role.Description = Description(request.Input.Description);
        store.Audit(request.ActorId, "role.updated", null, role.Id, null,
            JsonSerializer.Serialize(new { before, after = new { role.RoleName, role.Description } }));
        return Result<RoleDto>.Success(RoleDto.From(role));
    }, ct);

    public Task<Result<bool>> Handle(DeleteRoleCommand request, CancellationToken ct) => Run(request.ActorId, async () =>
    {
        var role = await store.FindRole(request.RoleId, ct);
        if (role is null) return Result<bool>.Failure(RbacErrors.RoleNotFound);
        if (RbacRules.IsSystem(role.RoleName)) return Result<bool>.Failure(RbacErrors.SystemRole);
        if (await store.RoleIsAssigned(role.Id, ct)) return Result<bool>.Failure(RbacErrors.AssignedRole);
        store.Audit(request.ActorId, "role.deleted", null, role.Id, null, JsonSerializer.Serialize(RoleDto.From(role)));
        store.DeleteRole(role);
        return Result<bool>.Success(true);
    }, ct);

    public Task<Result<bool>> Handle(SetRolePermissionCommand request, CancellationToken ct) => Run(request.ActorId, async () =>
    {
        var role = await store.FindRole(request.RoleId, ct);
        if (role is null) return Result<bool>.Failure(RbacErrors.RoleNotFound);
        var permission = await store.FindPermission(request.PermissionId, ct);
        if (permission is null) return Result<bool>.Failure(RbacErrors.PermissionNotFound);
        if (!request.Grant && role.RoleName == RoleNames.ItAdmin && permission.PermissionCode == PermissionCodes.Manage)
            return Result<bool>.Failure(RbacErrors.AdminPermission);
        var link = role.RolePermissions.SingleOrDefault(x => x.PermissionId == permission.Id);
        if (request.Grant && link is null) role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, Permission = permission });
        else if (!request.Grant && link is not null) store.DeletePermission(link);
        else return Result<bool>.Success(true);
        store.Audit(request.ActorId, request.Grant ? "permission.granted" : "permission.revoked", null, role.Id, permission.Id, "{}");
        return Result<bool>.Success(true);
    }, ct);

    public Task<Result<bool>> Handle(SetUserRoleCommand request, CancellationToken ct) => store.Exclusive(async () =>
    {
        var role = await store.FindRole(request.RoleId, ct);
        if (role is null) return Result<bool>.Failure(RbacErrors.RoleNotFound);
        if (!await store.IsAdministrator(request.ActorId, ct)
            && (role.RoleName != RoleNames.FrontDeskOfficer || !await scope.CanAssignFrontDesk(request.ActorId, request.UserId, ct)))
            return Result<bool>.Failure(RbacErrors.Forbidden);
        if (!await store.UserExists(request.UserId, ct)) return Result<bool>.Failure(RbacErrors.UserNotFound);
        var link = await store.FindAssignment(request.UserId, request.RoleId, ct);
        if (!request.Grant && link is not null && role.RoleName == RoleNames.ItAdmin && await store.IsLastActiveAdministrator(request.UserId, ct))
            return Result<bool>.Failure(RbacErrors.LastAdmin);
        if (request.Grant && link is null) store.AddAssignment(new UserRole { UserId = request.UserId, RoleId = request.RoleId });
        else if (!request.Grant && link is not null) store.DeleteAssignment(link);
        else return Result<bool>.Success(true);
        store.Audit(request.ActorId, request.Grant ? "role.assigned" : "role.revoked", request.UserId, request.RoleId, null, "{}");
        return Result<bool>.Success(true);
    }, ct);

    private static string? Description(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
