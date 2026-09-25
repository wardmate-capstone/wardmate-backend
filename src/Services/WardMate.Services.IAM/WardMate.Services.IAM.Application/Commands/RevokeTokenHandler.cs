using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;

namespace WardMate.Services.IAM.Application.Commands;

public sealed class RevokeTokenHandler(IIdentityStore store, ITokenService tokens) : IRequestHandler<RevokeTokenCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RevokeTokenCommand request, CancellationToken ct)
    {
        var token = await store.FindRefreshToken(tokens.HashRefreshToken(request.RefreshToken), ct);
        if (token is null || token.UserId != request.UserId) return Result<bool>.Failure(AuthErrors.InvalidToken);
        if (token.IsRevoked) return Result<bool>.Success(true);
        token.IsRevoked = true;
        var outcome = await store.SaveChanges(ct);
        return outcome == SaveOutcome.Duplicate ? Result<bool>.Failure(AuthErrors.InvalidToken) : Result<bool>.Success(true);
    }
}
