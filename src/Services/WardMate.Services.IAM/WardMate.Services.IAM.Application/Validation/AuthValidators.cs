using System.Text;
using FluentValidation;
using WardMate.Services.IAM.Application.Commands;

namespace WardMate.Services.IAM.Application.Validation;

public sealed class RegisterCitizenValidator : AbstractValidator<RegisterCitizenCommand>
{
    public RegisterCitizenValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100).Matches("^[a-zA-Z0-9_.-]+$");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Must(p => p is not null && Encoding.UTF8.GetByteCount(p) <= 72).WithMessage("Password must not exceed 72 UTF-8 bytes.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(255);
    }
}
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.UsernameOrEmail).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().Must(p => p is not null && Encoding.UTF8.GetByteCount(p) <= 72);
    }
}
public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty().MaximumLength(16384);
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(500);
    }
}
public sealed class RevokeTokenValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(500);
    }
}
