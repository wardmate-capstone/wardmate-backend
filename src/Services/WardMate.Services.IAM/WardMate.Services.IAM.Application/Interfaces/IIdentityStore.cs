using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Interfaces;

public enum SaveOutcome { Saved, Duplicate, ConcurrentUpdate }
public interface IIdentityStore
{
    Task<bool> AccountExists(string username, string email, CancellationToken ct);
    Task<User?> FindUser(string usernameOrEmail, CancellationToken ct);
    Task<User?> FindUser(Guid userId, CancellationToken ct);
    Task<Role> GetCitizenRole(CancellationToken ct);
    Task<RefreshToken?> FindRefreshToken(string tokenHash, CancellationToken ct);
    void AddUser(User user);
    void AddRefreshToken(RefreshToken token);
    Task<SaveOutcome> SaveChanges(CancellationToken ct);
}
