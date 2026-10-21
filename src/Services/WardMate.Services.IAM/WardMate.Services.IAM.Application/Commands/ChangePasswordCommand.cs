using System.Text;
using FluentValidation;
using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;
namespace WardMate.Services.IAM.Application.Commands;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword, string ConfirmNewPassword) : IRequest<Result<string>>;
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty().Must(p => p is not null && Encoding.UTF8.GetByteCount(p) <= 72);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Must(p => p is not null && p.Any(char.IsUpper)).WithMessage("Mật khẩu phải có chữ hoa.")
            .Must(p => p is not null && p.Any(char.IsLower)).WithMessage("Mật khẩu phải có chữ thường.")
            .Must(p => p is not null && p.Any(char.IsDigit)).WithMessage("Mật khẩu phải có chữ số.")
            .Must(p => p is not null && p.Any(c => char.IsPunctuation(c) || char.IsSymbol(c))).WithMessage("Mật khẩu phải có ký tự đặc biệt.")
            .Must(p => p is not null && Encoding.UTF8.GetByteCount(p) <= 72).WithMessage("Mật khẩu tối đa 72 byte UTF-8.");
        RuleFor(x => x.ConfirmNewPassword).NotEmpty().Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu không khớp.");
    }
}
public sealed class ChangePasswordHandler(IIdentityStore store, IPasswordHasher passwords, TimeProvider clock) : IRequestHandler<ChangePasswordCommand, Result<string>>
{
    public async Task<Result<string>> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await store.FindUser(request.UserId, ct);
        if (user is null || !user.IsActive) return Result<string>.Failure(AuthErrors.UserUnavailable);
        if (!passwords.Verify(request.CurrentPassword, user.PasswordHash))
            return Result<string>.Failure(new("iam.current_password_invalid", "Mật khẩu hiện tại không đúng.", ErrorKind.BadRequest));
        if (passwords.Verify(request.NewPassword, user.PasswordHash))
            return Result<string>.Failure(new("iam.password_unchanged", "Mật khẩu mới phải khác mật khẩu hiện tại.", ErrorKind.BadRequest));
        var result = await store.ChangePassword(user.Id, user.PasswordHash, passwords.Hash(request.NewPassword), clock.GetUtcNow().UtcDateTime, ct);
        return result == SaveOutcome.Saved
            ? Result<string>.Success("Đổi mật khẩu thành công. Vui lòng đăng nhập lại.")
            : Result<string>.Failure(new("iam.concurrent_update", "Tài khoản đã thay đổi. Vui lòng đăng nhập lại và thử lại.", ErrorKind.Conflict));
    }
}
