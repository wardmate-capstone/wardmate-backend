using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;

namespace WardMate.Services.IAM.Application.Commands;

public sealed class RefreshTokenHandler(IIdentityStore store, ITokenService tokens, TimeProvider clock)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var previous = await store.FindRefreshToken(tokens.HashRefreshToken(request.RefreshToken), ct);
        if (previous is null || previous.IsRevoked || previous.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
            return Result<AuthResponseDto>.Failure(AuthErrors.InvalidToken);
        var user = await store.FindUser(previous.UserId, ct);
        if (user is null || !user.IsActive) return Result<AuthResponseDto>.Failure(AuthErrors.InvalidToken);
        previous.IsRevoked = true;
        var response = TokenIssuer.Issue(user, store, tokens, clock);
        var outcome = await store.SaveChanges(ct);
        return outcome == SaveOutcome.Saved ? Result<AuthResponseDto>.Success(response) : Result<AuthResponseDto>.Failure(AuthErrors.InvalidToken);
    }
}
