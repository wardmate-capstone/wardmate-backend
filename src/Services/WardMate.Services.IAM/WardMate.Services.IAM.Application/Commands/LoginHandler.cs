using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;

namespace WardMate.Services.IAM.Application.Commands;

public sealed class LoginHandler(IIdentityStore store, IPasswordHasher passwords, ITokenService tokens, TimeProvider clock, ILoginThrottle throttle)
    : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken ct)
    {
        var login = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await store.FindUser(login, ct);
        // IAM-01a: đếm theo tài khoản để username và email của cùng người dùng chung một bộ đếm.
        var key = LoginThrottleKey.For(user?.Id, login);
        if (throttle.Blocked(key) is { } remaining)
            return Result<AuthResponseDto>.Failure(AuthErrors.LoginLocked(remaining));
        if (user is null || !user.IsActive || !passwords.Verify(request.Password, user.PasswordHash))
        {
            throttle.Failed(key);
            return Result<AuthResponseDto>.Failure(AuthErrors.InvalidCredentials);
        }
        throttle.Reset(key);
        var response = TokenIssuer.Issue(user, store, tokens, clock);
        var outcome = await store.SaveChanges(ct);
        return outcome == SaveOutcome.Saved ? Result<AuthResponseDto>.Success(response) : Result<AuthResponseDto>.Failure(AuthErrors.InvalidToken);
    }
}

public static class LoginThrottleKey
{
    public static string For(Guid? userId, string normalizedLogin) =>
        userId is { } id ? "user:" + id.ToString("N") : "login:" + normalizedLogin;
}
