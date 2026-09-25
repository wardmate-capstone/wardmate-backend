using System.Text;
using FluentValidation;
using WardMate.Services.IAM.Application.Commands;

namespace WardMate.Services.IAM.Application.Validation;

public sealed class RegisterCitizenValidator : AbstractValidator<RegisterCitizenCommand>
{
    public RegisterCitizenValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .MaximumLength(100).WithMessage("Tên đăng nhập không được vượt quá 100 ký tự.")
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Tên đăng nhập chỉ được chứa chữ cái không dấu, chữ số, dấu gạch dưới, dấu chấm và dấu gạch ngang.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Địa chỉ email không hợp lệ.")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Must(p => p is not null && p.Any(char.IsUpper)).WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết hoa.")
            .Must(p => p is not null && p.Any(c => char.IsPunctuation(c) || char.IsSymbol(c))).WithMessage("Mật khẩu phải chứa ít nhất một ký tự đặc biệt (dấu câu hoặc ký hiệu).")
            .Must(p => p is not null && Encoding.UTF8.GetByteCount(p) <= 72).WithMessage("Mật khẩu không được vượt quá 72 byte UTF-8.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(255).WithMessage("Họ và tên không được vượt quá 255 ký tự.");
    }
}
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.UsernameOrEmail).NotEmpty().WithMessage("Tên đăng nhập hoặc email không được để trống.")
            .MaximumLength(255).WithMessage("Tên đăng nhập hoặc email không được vượt quá 255 ký tự.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .Must(p => p is not null && Encoding.UTF8.GetByteCount(p) <= 72).WithMessage("Mật khẩu không được vượt quá 72 byte UTF-8.");
    }
}
public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty().WithMessage("Mã truy cập không được để trống.")
            .MaximumLength(16384).WithMessage("Mã truy cập không được vượt quá 16384 ký tự.");
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Mã làm mới không được để trống.")
            .MaximumLength(500).WithMessage("Mã làm mới không được vượt quá 500 ký tự.");
    }
}
public sealed class RevokeTokenValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không được để trống.");
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Mã làm mới không được để trống.")
            .MaximumLength(500).WithMessage("Mã làm mới không được vượt quá 500 ký tự.");
    }
}
