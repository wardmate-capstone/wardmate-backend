using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Accounts;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class AccountStore(IamDbContext db, TimeProvider clock) : IAccountStore
{
    public async Task<AccountPage> List(int page, int pageSize, CancellationToken ct)
    {
        var total = await db.Users.CountAsync(ct);
        var items = await db.Users.AsNoTracking().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AccountDto(x.Id, x.Username, x.Email, x.IsActive, x.CreatedAt)).ToArrayAsync(ct);
        return new(items, page, pageSize, total);
    }

    public Task<AccountDto?> Get(Guid userId, CancellationToken ct) => db.Users.AsNoTracking().Where(x => x.Id == userId)
        .Select(x => new AccountDto(x.Id, x.Username, x.Email, x.IsActive, x.CreatedAt)).SingleOrDefaultAsync(ct);

    public async Task<bool> SetActive(Guid userId, bool active, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var updated = await db.Users.Where(x => x.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, active).SetProperty(x => x.UpdatedAt, now), ct);
        if (!active)
            await db.RefreshTokens.Where(x => x.UserId == userId && !x.IsRevoked)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsRevoked, true), ct);
        await transaction.CommitAsync(ct);
        return updated > 0;
    }
}
