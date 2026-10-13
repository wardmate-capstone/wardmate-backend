using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Rbac;
using WardMate.Services.IAM.Domain;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class UserDirectory(IamDbContext db, ManagementScope scope, IRbacStore rbac) : IUserDirectory
{
    public async Task<Result<DirectoryPage>> List(DirectoryQuery r, CancellationToken ct)
    {
        var actor = await Access(r.ActorId, ct);
        if (actor is null || !actor.Permissions.Contains("iam.accounts.read") ||
            (r.Admin ? !actor.Roles.Contains(RoleNames.ItAdmin) : !actor.Roles.Contains(RoleNames.Manager) || actor.WardCode is null))
            return Result<DirectoryPage>.Failure(RbacErrors.Forbidden);
        var q = r.Admin ? db.Users.AsNoTracking() : scope.VisibleUsers(r.ActorId).AsNoTracking()
            .Where(x => x.UserRoles.Any(ur => ur.Role.RoleName == RoleNames.FrontDeskOfficer));
        // Explicit ward filtering never expands the manager's server-side scope.
        var f = r.Filter;
        if (!string.IsNullOrWhiteSpace(f.WardCode)) { var ward = f.WardCode.Trim().ToUpperInvariant(); q = q.Where(x => x.Ward != null && x.Ward.Code == ward); }
        if (f.AssignedCategory is not null) q = q.Where(x => x.AssignedCategories.Contains(f.AssignedCategory.Value));
        if (!string.IsNullOrWhiteSpace(f.Role)) { var role = f.Role.Trim().ToUpperInvariant(); q = q.Where(x => x.UserRoles.Any(ur => ur.Role.RoleName == role)); }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var pattern = "%" + f.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(x => EF.Functions.ILike(x.Email, pattern, "\\") || x.Profile != null &&
                (EF.Functions.ILike(x.Profile.FullName, pattern, "\\") || x.Profile.IdentityNumber != null && EF.Functions.ILike(x.Profile.IdentityNumber, pattern, "\\")));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Username).ThenBy(x => x.Id).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)
            .Select(x => new DirectoryUser(x.Id, x.Username, x.Email, x.Profile == null ? null : x.Profile.FullName,
                x.Profile == null ? null : x.Profile.IdentityNumber, x.IsActive, x.Ward == null ? null : x.Ward.Code,
                x.UserRoles.Select(ur => ur.Role.RoleName).ToArray(), x.AssignedCategories)).ToArrayAsync(ct);
        return Result<DirectoryPage>.Success(new(items, f.Page, f.PageSize, total));
    }
    public Task<Result<bool>> Assign(AssignCategoriesCommand r, CancellationToken ct) => rbac.Exclusive(async () =>
    {
        var actor = await Access(r.ActorId, ct);
        if (actor is null || !actor.Permissions.Contains("iam.accounts.manage")) return Result<bool>.Failure(RbacErrors.Forbidden);
        var user = await scope.VisibleUsers(r.ActorId).SingleOrDefaultAsync(x => x.Id == r.UserId &&
            x.UserRoles.Any(ur => ur.Role.RoleName == RoleNames.FrontDeskOfficer), ct);
        if (user is null) return Result<bool>.Failure(RbacErrors.UserNotFound);
        user.AssignedCategories = r.Categories.Order().ToArray(); user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Result<bool>.Success(true);
    }, ct);
    public async Task<AccessContextDto?> Access(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(x => x.Ward).Include(x => x.UserRoles)
            .ThenInclude(x => x.Role).ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        return user is null ? null : new(user.Id, user.Ward?.Code, user.UserRoles.Select(x => x.Role.RoleName).Distinct().ToArray(),
            user.UserRoles.SelectMany(x => x.Role.RolePermissions).Select(x => x.Permission.PermissionCode).Distinct().ToArray());
    }
    public Task<WardDto[]> Wards(CancellationToken ct) => db.Wards.AsNoTracking().OrderBy(x => x.Code).Select(x => new WardDto(x.Id, x.Code, x.Name)).ToArrayAsync(ct);
}
