using Microsoft.EntityFrameworkCore;
using Npgsql;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class IdentityStore(IamDbContext db) : IIdentityStore
{
    private IQueryable<User> Users => db.Users.Include(x => x.Profile)
        .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission);
    public Task<bool> AccountExists(string username, string email, CancellationToken ct)
        => db.Users.AnyAsync(x => x.Username == username || x.Email == email, ct);
    public Task<User?> FindUser(string usernameOrEmail, CancellationToken ct)
        => Users.SingleOrDefaultAsync(x => x.Username == usernameOrEmail || x.Email == usernameOrEmail, ct);
    public Task<User?> FindUser(Guid userId, CancellationToken ct) => Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
    public Task<Role> GetCitizenRole(CancellationToken ct) => db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission)
        .SingleAsync(x => x.RoleName == RoleNames.RegisteredCitizen, ct);
    public Task<RefreshToken?> FindRefreshToken(string tokenHash, CancellationToken ct)
        => db.RefreshTokens.SingleOrDefaultAsync(x => x.Token == tokenHash, ct);
    public void QueueWelcome(User user) => db.WelcomeEmails.Add(new WelcomeEmail { UserId = user.Id, NextAttemptAtUtc = user.CreatedAt });
    public void AddUser(User user) => db.Users.Add(user);
    public void AddRefreshToken(RefreshToken token) => db.RefreshTokens.Add(token);
    public async Task<SaveOutcome> ChangePassword(Guid userId, string expectedHash, string newHash, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (current is null || !current.IsActive || current.PasswordHash != expectedHash) return SaveOutcome.ConcurrentUpdate;
        await db.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, newHash).SetProperty(x => x.UpdatedAt, now), ct);
        await db.RefreshTokens.Where(x => x.UserId == userId && !x.IsRevoked).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRevoked, true), ct);
        await db.UserOtps.Where(x => x.UserId == userId && !x.IsUsed && x.ExpiresAtUtc > now).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, now), ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return SaveOutcome.Saved;
    }

    public async Task<SaveOutcome> SaveChanges(CancellationToken ct)
    {
        // Managed profile writes already run inside the scope-check transaction.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Serialize token issuance with account disabling so no usable token survives a ban.
            var owners = db.ChangeTracker.Entries<RefreshToken>().Where(x => x.State == EntityState.Added)
                .Select(x => x.Entity.UserId).Distinct().Order().ToArray();
            foreach (var owner in owners)
            {
                var current = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {owner} FOR UPDATE")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (current is null || !current.IsActive || db.Users.Local.Any(x => x.Id == owner && x.PasswordHash != current.PasswordHash))
                {
                    db.ChangeTracker.Clear();
                    return SaveOutcome.ConcurrentUpdate;
                }
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return SaveOutcome.ConcurrentUpdate; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); return SaveOutcome.Duplicate; }
    }
}
