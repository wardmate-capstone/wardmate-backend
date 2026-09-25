using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
public sealed record AccessToken(string Value, DateTime ExpiresAt);
public sealed record NewRefreshToken(string Value, string Hash, DateTime ExpiresAt);
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    NewRefreshToken CreateRefreshToken();
    string HashRefreshToken(string token);
    Guid? ValidateAccessTokenForRefresh(string token);
}
