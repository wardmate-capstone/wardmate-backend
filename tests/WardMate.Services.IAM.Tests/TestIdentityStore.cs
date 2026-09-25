using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Tests;

internal sealed class TestIdentityStore : IIdentityStore
{
    public List<User> Users { get; } = [];
    public List<RefreshToken> Tokens { get; } = [];
    public SaveOutcome Outcome { get; set; } = SaveOutcome.Saved;
    public Task<bool> AccountExists(string username, string email, CancellationToken ct) => Task.FromResult(Users.Any(x => x.Username == username || x.Email == email));
    public Task<User?> FindUser(string identity, CancellationToken ct) => Task.FromResult(Users.SingleOrDefault(x => x.Username == identity || x.Email == identity));
    public Task<User?> FindUser(Guid id, CancellationToken ct) => Task.FromResult(Users.SingleOrDefault(x => x.Id == id));
    public Task<Role> GetCitizenRole(CancellationToken ct) => Task.FromResult(CitizenRole());
    public Task<RefreshToken?> FindRefreshToken(string hash, CancellationToken ct) => Task.FromResult(Tokens.SingleOrDefault(x => x.Token == hash));
    public void AddUser(User user) => Users.Add(user);
    public void AddRefreshToken(RefreshToken token) => Tokens.Add(token);
    public Task<SaveOutcome> SaveChanges(CancellationToken ct) => Task.FromResult(Outcome);
    public static Role CitizenRole() => new()
    {
        Id = 1,
        RoleName = RoleNames.RegisteredCitizen,
        RolePermissions = [new RolePermission { RoleId = 1, PermissionId = 1, Permission = new Permission { Id = 1, PermissionCode = "iam.profile.read", PermissionName = "Read own profile", Module = "IAM" } }]
    };
}
