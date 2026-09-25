using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Commands;

internal static class TokenIssuer
{
    // Caller commits once, together with any revocation, to keep rotation atomic.
    public static AuthResponseDto Issue(User user, IIdentityStore store, ITokenService tokens, TimeProvider clock)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        store.AddRefreshToken(new RefreshToken { UserId = user.Id, Token = refresh.Hash, ExpiresAt = refresh.ExpiresAt, CreatedAt = clock.GetUtcNow().UtcDateTime });
        return new(access.Value, access.ExpiresAt, refresh.Value, refresh.ExpiresAt);
    }
}
