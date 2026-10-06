using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Rbac;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class ManagementScope(IamDbContext db, IRbacStore rbac) : IManagementScope
{
    private IQueryable<User> Administrators(Guid actorId) => db.Users.Where(a => a.Id == actorId && a.IsActive
        && a.UserRoles.Any(r => r.Role.RoleName == RoleNames.ItAdmin
            && r.Role.RolePermissions.Any(p => p.Permission.PermissionCode == PermissionCodes.Manage)));

    private IQueryable<User> Managers(Guid actorId) => db.Users.Where(a => a.Id == actorId && a.IsActive
        && a.WardId != null && a.UserRoles.Any(r => r.Role.RoleName == RoleNames.Manager));

    // Applied before count/paging and lookup. Never accept the actor's ward from request or JWT.
    public IQueryable<User> VisibleUsers(Guid actorId)
    {
        var administrators = Administrators(actorId);
        var managers = Managers(actorId);
        return db.Users.Where(target =>
        administrators.Any() || (target.Id != actorId
            && managers.Any(a => a.WardId == target.WardId)
            && target.UserRoles.Any(r => r.Role.RoleName == RoleNames.FrontDeskOfficer)
            && !target.UserRoles.Any(r => r.Role.RoleName != RoleNames.FrontDeskOfficer
                && r.Role.RoleName != RoleNames.RegisteredCitizen)));
    }

    public async Task<bool> CanManage(Guid actorId, CancellationToken ct) =>
        await Administrators(actorId).AnyAsync(ct) || await Managers(actorId).AnyAsync(ct);

    public Task<bool> CanAssignFrontDesk(Guid actorId, Guid userId, CancellationToken ct)
    {
        var managers = Managers(actorId);
        return db.Users.AnyAsync(target => target.Id == userId && target.Id != actorId
            && managers.Any(a => a.WardId == target.WardId)
            && !target.UserRoles.Any(r => r.Role.RoleName != RoleNames.FrontDeskOfficer
                && r.Role.RoleName != RoleNames.RegisteredCitizen), ct);
    }

    public Task<Result<T>> Execute<T>(Guid actorId, Guid userId, Func<Task<Result<T>>> operation, CancellationToken ct) =>
        rbac.Exclusive(async () =>
        {
            if (!await CanManage(actorId, ct)) return Result<T>.Failure(RbacErrors.Forbidden);
            if (!await VisibleUsers(actorId).AnyAsync(x => x.Id == userId, ct))
                return Result<T>.Failure(RbacErrors.UserNotFound);
            return await operation();
        }, ct);
}
