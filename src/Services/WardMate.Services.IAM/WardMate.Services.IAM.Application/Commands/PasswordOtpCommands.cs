using FluentValidation;
using MediatR;
using WardMate.Services.IAM.Application.Common;
namespace WardMate.Services.IAM.Application.Commands;

public sealed record RequestPasswordOtpCommand(string EmailOrUsername, string Channel = "EMAIL", string Purpose = "FORGOT_PASSWORD", Guid? ActorId = null) : IRequest<Result<string>>;
public sealed record VerifyPasswordOtpCommand(string EmailOrUsername, string Otp, string NewPassword, string ConfirmNewPassword,
    string Purpose = "FORGOT_PASSWORD", Guid? ActorId = null) : IRequest<Result<string>>;
public interface IPasswordOtpService
{
    Task<Result<string>> Request(RequestPasswordOtpCommand command, CancellationToken ct);
    Task<Result<string>> Verify(VerifyPasswordOtpCommand command, CancellationToken ct);
}
public interface IIamEmailSender
{
    bool Enabled { get; }
    Task Welcome(string email, string fullName, string username, CancellationToken ct);
    Task Otp(string email, string code, CancellationToken ct);
}
public sealed class RequestPasswordOtpValidator : AbstractValidator<RequestPasswordOtpCommand>
{
    public RequestPasswordOtpValidator()
    {
        RuleFor(x => x.EmailOrUsername).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Channel).Must(x => x is "EMAIL" or "SMS");
        RuleFor(x => x.Purpose).Must(x => x is "CHANGE_PASSWORD" or "FORGOT_PASSWORD");
    }
}
public sealed class VerifyPasswordOtpValidator : AbstractValidator<VerifyPasswordOtpCommand>
{
    public VerifyPasswordOtpValidator()
    {
        RuleFor(x => x.EmailOrUsername).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Otp).NotEmpty().Matches("^[0-9]{6}$");
        RuleFor(x => x.Purpose).Must(x => x is "CHANGE_PASSWORD" or "FORGOT_PASSWORD");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Must(p => p is not null && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit)
                && p.Any(c => char.IsPunctuation(c) || char.IsSymbol(c)) && System.Text.Encoding.UTF8.GetByteCount(p) <= 72)
            .WithMessage("Mật khẩu cần chữ hoa, chữ thường, số, ký tự đặc biệt và tối đa 72 byte UTF-8.");
        RuleFor(x => x.ConfirmNewPassword).NotEmpty().Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu không khớp.");
    }
}
public sealed class RequestPasswordOtpHandler(IPasswordOtpService service) : IRequestHandler<RequestPasswordOtpCommand, Result<string>>
{
    public Task<Result<string>> Handle(RequestPasswordOtpCommand request, CancellationToken ct) => service.Request(request, ct);
}
public sealed class VerifyPasswordOtpHandler(IPasswordOtpService service) : IRequestHandler<VerifyPasswordOtpCommand, Result<string>>
{
    public Task<Result<string>> Handle(VerifyPasswordOtpCommand request, CancellationToken ct) => service.Verify(request, ct);
}
