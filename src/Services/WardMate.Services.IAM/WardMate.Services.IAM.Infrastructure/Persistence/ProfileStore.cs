using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Profiles;
using WardMate.Services.IAM.Application.Rbac;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class ProfileStore(IamDbContext db, IIdentityStore identity, ManagementScope scope) : IProfileStore
{
    public async Task<Result<ProfilePage>> List(Guid actorId, int page, int pageSize, CancellationToken ct)
    {
        if (!await scope.CanManage(actorId, ct)) return Result<ProfilePage>.Failure(RbacErrors.Forbidden);
        var visibleUsers = scope.VisibleUsers(actorId);
        var query = db.UserProfiles.AsNoTracking().Where(p => visibleUsers.Any(u => u.Id == p.UserId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.FullName).ThenBy(p => p.UserId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProfileListItemDto(p.UserId, p.FullName, p.IdentityNumber, p.PhoneNumber,
                p.DateOfBirth, p.Gender, p.PermanentAddress, p.TemporaryAddress, p.UpdatedAt)).ToArrayAsync(ct);
        return Result<ProfilePage>.Success(new(items, page, pageSize, total));
    }

    public Task<bool> UserExists(Guid userId, CancellationToken ct) => db.Users.AnyAsync(x => x.Id == userId, ct);
    public Task<UserProfile?> Find(Guid userId, CancellationToken ct) => db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public void Add(UserProfile profile) => db.UserProfiles.Add(profile);
    public void Remove(UserProfile profile) => db.UserProfiles.Remove(profile);
    public Task<SaveOutcome> SaveChanges(CancellationToken ct) => identity.SaveChanges(ct);
}
