using FluentValidation;
using MediatR;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Validation;

namespace WardMate.Services.IAM.Application.Accounts;

public sealed record CreateManagerInput(string Username, string Email, string Password, string FullName, Guid? WardId);
public sealed record CreateProcedureManagerInput(string Username, string Email, string Password, string FullName, Guid? WardId);
public sealed record CreateManagerCommand(Guid ActorId, CreateManagerInput Input) : IRequest<Result<ManagedUserDto>>;
public sealed record CreateProcedureManagerCommand(Guid ActorId, CreateProcedureManagerInput Input) : IRequest<Result<ManagedUserDto>>;

public sealed class ProvisionStaffHandlers(IStaffAdministration store) :
    IRequestHandler<CreateManagerCommand, Result<ManagedUserDto>>,
    IRequestHandler<CreateProcedureManagerCommand, Result<ManagedUserDto>>
{
    public Task<Result<ManagedUserDto>> Handle(CreateManagerCommand r, CancellationToken ct) => store.CreateManager(r.ActorId, r.Input, ct);
    public Task<Result<ManagedUserDto>> Handle(CreateProcedureManagerCommand r, CancellationToken ct) => store.CreateProcedureManager(r.ActorId, r.Input, ct);
}

// Keep the existing front-desk/registration validation unchanged. These two new
// contracts additionally require lowercase and a digit as requested by FE.
internal sealed class ProvisionStaffCredentialsValidator : AbstractValidator<RegisterCitizenCommand>
{
    public ProvisionStaffCredentialsValidator()
    {
        Include(new RegisterCitizenValidator());
        RuleFor(x => x.Password).Must(p => p is not null && p.Any(char.IsLower))
            .WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết thường.");
        RuleFor(x => x.Password).Must(p => p is not null && p.Any(char.IsDigit))
            .WithMessage("Mật khẩu phải chứa ít nhất một chữ số.");
    }
}

public sealed class CreateManagerValidator : AbstractValidator<CreateManagerCommand>
{
    public CreateManagerValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Thông tin tài khoản không được để trống.");
        When(x => x.Input is not null, () => RuleFor(x => new RegisterCitizenCommand(
            x.Input.Username, x.Input.Email, x.Input.Password, x.Input.FullName)).SetValidator(new ProvisionStaffCredentialsValidator()));
    }
}

public sealed class CreateProcedureManagerValidator : AbstractValidator<CreateProcedureManagerCommand>
{
    public CreateProcedureManagerValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Thông tin tài khoản không được để trống.");
        When(x => x.Input is not null, () => RuleFor(x => new RegisterCitizenCommand(
            x.Input.Username, x.Input.Email, x.Input.Password, x.Input.FullName)).SetValidator(new ProvisionStaffCredentialsValidator()));
    }
}

