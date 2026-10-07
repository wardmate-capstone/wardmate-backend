using FluentValidation;
using MediatR;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Validation;

namespace WardMate.Services.IAM.Application.Accounts;

public sealed record ListUsersQuery(Guid ActorId, int Page = 1, int PageSize = 20) : IRequest<Result<ManagedUserPage>>;
public sealed record CreateFrontDeskCommand(Guid ActorId, CreateFrontDeskInput Input) : IRequest<Result<ManagedUserDto>>;
public sealed record ListWardsQuery(Guid ActorId) : IRequest<Result<WardDto[]>>;
public sealed record CreateWardCommand(Guid ActorId, string Code, string Name) : IRequest<Result<WardDto>>;
public sealed record AssignWardCommand(Guid ActorId, Guid UserId, Guid? WardId) : IRequest<Result<bool>>;

public sealed class StaffHandlers(IStaffAdministration store) :
    IRequestHandler<ListUsersQuery, Result<ManagedUserPage>>,
    IRequestHandler<CreateFrontDeskCommand, Result<ManagedUserDto>>,
    IRequestHandler<ListWardsQuery, Result<WardDto[]>>,
    IRequestHandler<CreateWardCommand, Result<WardDto>>,
    IRequestHandler<AssignWardCommand, Result<bool>>
{
    public Task<Result<ManagedUserPage>> Handle(ListUsersQuery r, CancellationToken ct) => store.ListUsers(r.ActorId, r.Page, r.PageSize, ct);
    public Task<Result<ManagedUserDto>> Handle(CreateFrontDeskCommand r, CancellationToken ct) => store.CreateFrontDesk(r.ActorId, r.Input, ct);
    public Task<Result<WardDto[]>> Handle(ListWardsQuery r, CancellationToken ct) => store.ListWards(r.ActorId, ct);
    public Task<Result<WardDto>> Handle(CreateWardCommand r, CancellationToken ct) => store.CreateWard(r.ActorId, r.Code, r.Name, ct);
    public Task<Result<bool>> Handle(AssignWardCommand r, CancellationToken ct) => store.AssignWard(r.ActorId, r.UserId, r.WardId, ct);
}

public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000).WithMessage("Số trang phải từ 1 đến 1000000.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số người dùng mỗi trang phải từ 1 đến 100.");
    }
}

public sealed class CreateFrontDeskValidator : AbstractValidator<CreateFrontDeskCommand>
{
    public CreateFrontDeskValidator()
    {
        RuleFor(x => x.Input).NotNull().WithMessage("Thông tin tài khoản không được để trống.");
        When(x => x.Input is not null, () => RuleFor(x => new RegisterCitizenCommand(
            x.Input.Username, x.Input.Email, x.Input.Password, x.Input.FullName)).SetValidator(new RegisterCitizenValidator()));
    }
}

public sealed class CreateWardValidator : AbstractValidator<CreateWardCommand>
{
    public CreateWardValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Mã phường không được để trống.")
            .MaximumLength(50).WithMessage("Mã phường không được vượt quá 50 ký tự.")
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("Mã phường chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên phường không được để trống.")
            .MaximumLength(255).WithMessage("Tên phường không được vượt quá 255 ký tự.");
    }
}
