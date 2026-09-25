using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;

namespace WardMate.Services.IAM.Application.Commands;

public sealed class LoginHandler(IIdentityStore store, IPasswordHasher passwords, ITokenService tokens, TimeProvider clock)
    : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await store.FindUser(request.UsernameOrEmail.Trim().ToLowerInvariant(), ct);
        if (user is null || !user.IsActive || !passwords.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponseDto>.Failure(AuthErrors.InvalidCredentials);
        var response = TokenIssuer.Issue(user, store, tokens, clock);
        var outcome = await store.SaveChanges(ct);
        return outcome == SaveOutcome.Saved ? Result<AuthResponseDto>.Success(response) : Result<AuthResponseDto>.Failure(AuthErrors.InvalidToken);
    }
}
