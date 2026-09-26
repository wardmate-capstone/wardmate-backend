using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class ProfileStore(IamDbContext db, IIdentityStore identity) : IProfileStore
{
    public Task<bool> UserExists(Guid userId, CancellationToken ct) => db.Users.AnyAsync(x => x.Id == userId, ct);
    public Task<UserProfile?> Find(Guid userId, CancellationToken ct) => db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public void Add(UserProfile profile) => db.UserProfiles.Add(profile);
    public void Remove(UserProfile profile) => db.UserProfiles.Remove(profile);
    public Task<SaveOutcome> SaveChanges(CancellationToken ct) => identity.SaveChanges(ct);
}
