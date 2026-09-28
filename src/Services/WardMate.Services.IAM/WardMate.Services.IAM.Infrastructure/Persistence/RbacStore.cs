using Microsoft.EntityFrameworkCore;
using Npgsql;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Rbac;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class RbacStore(IamDbContext db, TimeProvider clock) : IRbacStore
{
    public async Task<Result<T>> Exclusive<T>(Func<Task<Result<T>>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // One database-scoped transaction lock also covers account disabling. No in-process lock.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1464680777)", ct);
        // Authentication may have tracked the actor before this transaction acquired the lock.
        db.ChangeTracker.Clear();
        try
        {
            var result = await operation();
            if (!result.IsSuccess)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return result;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Result<T>.Failure(RbacErrors.Duplicate);
        }
    }

    public Task<bool> IsAdministrator(Guid userId, CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive
        && x.UserRoles.Any(r => r.Role.RoleName == RoleNames.ItAdmin
            && r.Role.RolePermissions.Any(p => p.Permission.PermissionCode == PermissionCodes.Manage)), ct);

    public Task<bool> HasPermission(Guid userId, string permission, CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive
        && x.UserRoles.Any(r => r.Role.RolePermissions.Any(p => p.Permission.PermissionCode == permission)), ct);

    public async Task<bool> IsLastActiveAdministrator(Guid userId, CancellationToken ct)
    {
        var admins = db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.RoleName == RoleNames.ItAdmin));
        return await admins.AnyAsync(x => x.Id == userId, ct) && !await admins.AnyAsync(x => x.Id != userId, ct);
    }

    private IQueryable<Role> Roles => db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission);
    public Task<Role?> FindRole(int id, CancellationToken ct) => Roles.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Permission?> FindPermission(int id, CancellationToken ct) => db.Permissions.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> UserExists(Guid id, CancellationToken ct) => db.Users.AnyAsync(x => x.Id == id, ct);
    public Task<bool> RoleIsAssigned(int id, CancellationToken ct) => db.UserRoles.AnyAsync(x => x.RoleId == id, ct);
    public Task<UserRole?> FindAssignment(Guid userId, int roleId, CancellationToken ct) => db.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, ct);

    public async Task<RbacPage<RoleDto>> ListRoles(int page, int pageSize, CancellationToken ct)
    {
        var total = await db.Roles.CountAsync(ct);
        var roles = await Roles.AsNoTracking().OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(roles.Select(RoleDto.From).ToArray(), page, pageSize, total);
    }
    public Task<PermissionDto[]> ListPermissions(CancellationToken ct) => db.Permissions.AsNoTracking().OrderBy(x => x.Id)
        .Select(x => new PermissionDto(x.Id, x.PermissionCode, x.PermissionName, x.Module)).ToArrayAsync(ct);
    public async Task<RoleDto[]> UserRoles(Guid userId, CancellationToken ct)
    {
        var roleIds = db.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId);
        return (await Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).OrderBy(x => x.Id).ToArrayAsync(ct)).Select(RoleDto.From).ToArray();
    }
    public async Task<RbacPage<AuditDto>> ListAudit(int page, int pageSize, CancellationToken ct)
    {
        var total = await db.RbacAuditLogs.CountAsync(ct);
        var items = await db.RbacAuditLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditDto(x.Id, x.ActorUserId, x.Action, x.TargetUserId, x.RoleId, x.PermissionId, x.Details, x.CreatedAt)).ToArrayAsync(ct);
        return new(items, page, pageSize, total);
    }
    public void AddRole(Role role) => db.Roles.Add(role);
    public void DeleteRole(Role role) => db.Roles.Remove(role);
    public void AddAssignment(UserRole assignment) => db.UserRoles.Add(assignment);
    public void DeleteAssignment(UserRole assignment) => db.UserRoles.Remove(assignment);
    public void DeletePermission(RolePermission permission) => db.RolePermissions.Remove(permission);
    public void Audit(Guid actorId, string action, Guid? userId, int? roleId, int? permissionId, string details) => db.RbacAuditLogs.Add(new()
    {
        ActorUserId = actorId, Action = action, TargetUserId = userId, RoleId = roleId, PermissionId = permissionId,
        Details = details, CreatedAt = clock.GetUtcNow().UtcDateTime
    });
    public Task Save(CancellationToken ct) => db.SaveChangesAsync(ct);
}
