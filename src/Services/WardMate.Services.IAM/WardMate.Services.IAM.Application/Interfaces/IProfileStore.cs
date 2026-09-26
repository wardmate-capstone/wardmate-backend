using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Interfaces;

public interface IProfileStore
{
    Task<bool> UserExists(Guid userId, CancellationToken ct);
    Task<UserProfile?> Find(Guid userId, CancellationToken ct);
    void Add(UserProfile profile);
    void Remove(UserProfile profile);
    Task<SaveOutcome> SaveChanges(CancellationToken ct);
}
