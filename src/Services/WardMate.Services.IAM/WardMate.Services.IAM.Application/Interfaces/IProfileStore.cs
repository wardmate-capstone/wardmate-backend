using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Profiles;

namespace WardMate.Services.IAM.Application.Interfaces;

public interface IProfileStore
{
    Task<Result<ProfilePage>> List(Guid actorId, int page, int pageSize, CancellationToken ct);
    Task<bool> UserExists(Guid userId, CancellationToken ct);
    Task<UserProfile?> Find(Guid userId, CancellationToken ct);
    void Add(UserProfile profile);
    void Remove(UserProfile profile);
    Task<SaveOutcome> SaveChanges(CancellationToken ct);
}
