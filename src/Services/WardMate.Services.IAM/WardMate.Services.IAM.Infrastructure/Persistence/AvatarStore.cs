using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Application.Profiles;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class AvatarStore(IamDbContext db, IIdentityStore identity) : IAvatarStore
{
    public Task<User?> FindUser(Guid userId, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
    public Task<UserAvatar?> Find(Guid userId, CancellationToken ct) => db.UserAvatars.SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public void Add(UserAvatar avatar) => db.UserAvatars.Add(avatar);
    public void Remove(UserAvatar avatar) => db.UserAvatars.Remove(avatar);
    public Task<bool> IsStaff(Guid userId, CancellationToken ct) =>
        db.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.RoleName != RoleNames.RegisteredCitizen, ct);
    public Task<SaveOutcome> SaveChanges(CancellationToken ct) => identity.SaveChanges(ct);
}
