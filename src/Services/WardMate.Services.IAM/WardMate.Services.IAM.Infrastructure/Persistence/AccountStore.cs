using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Rbac;
using WardMate.Services.IAM.Domain;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class AccountStore(IamDbContext db, TimeProvider clock, IRbacStore rbac, ManagementScope scope) : IAccountStore
{
    public async Task<Result<AccountPage>> List(Guid actorId, int page, int pageSize, CancellationToken ct)
    {
        if (!await scope.CanManage(actorId, ct)) return Result<AccountPage>.Failure(RbacErrors.Forbidden);
        var query = scope.VisibleUsers(actorId).AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AccountDto(x.Id, x.Username, x.Email, x.IsActive, x.CreatedAt, x.WardId)).ToArrayAsync(ct);
        return Result<AccountPage>.Success(new(items, page, pageSize, total));
    }

    public async Task<Result<AccountDto>> Get(Guid actorId, Guid userId, CancellationToken ct)
    {
        if (!await scope.CanManage(actorId, ct)) return Result<AccountDto>.Failure(RbacErrors.Forbidden);
        var account = await scope.VisibleUsers(actorId).AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new AccountDto(x.Id, x.Username, x.Email, x.IsActive, x.CreatedAt, x.WardId)).SingleOrDefaultAsync(ct);
        return account is null ? Result<AccountDto>.Failure(RbacErrors.UserNotFound) : Result<AccountDto>.Success(account);
    }

    public Task<Result<bool>> SetActive(Guid actorId, Guid userId, bool active, CancellationToken ct) => scope.Execute(actorId, userId, async () =>
    {
        if (!await rbac.UserExists(userId, ct)) return Result<bool>.Failure(RbacErrors.UserNotFound);
        if (!active && await rbac.IsLastActiveAdministrator(userId, ct)) return Result<bool>.Failure(RbacErrors.LastAdmin);
        var now = clock.GetUtcNow().UtcDateTime;
        var updated = await db.Users.Where(x => x.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, active).SetProperty(x => x.UpdatedAt, now), ct);
        if (!active)
            await db.RefreshTokens.Where(x => x.UserId == userId && !x.IsRevoked)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsRevoked, true), ct);
        rbac.Audit(actorId, active ? "account.enabled" : "account.disabled", userId, null, null, "{}");
        return Result<bool>.Success(updated > 0);
    }, ct);
}
