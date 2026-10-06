using FluentValidation;
using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Application.Profiles;

namespace WardMate.Services.IAM.Application.Accounts;

public sealed record AccountDto(Guid Id, string Username, string Email, bool IsActive, DateTime CreatedAt, Guid? WardId = null);
public sealed record AccountPage(AccountDto[] Items, int Page, int PageSize, int Total);
public sealed record ListAccountsQuery(Guid ActorId, int Page = 1, int PageSize = 20) : IRequest<Result<AccountPage>>;
public sealed record GetAccountQuery(Guid ActorId, Guid UserId) : IRequest<Result<AccountDto>>;
public sealed record SetAccountStatusCommand(Guid ActorId, Guid UserId, bool IsActive) : IRequest<Result<bool>>;

public interface IAccountStore
{
    Task<Result<AccountPage>> List(Guid actorId, int page, int pageSize, CancellationToken ct);
    Task<Result<AccountDto>> Get(Guid actorId, Guid userId, CancellationToken ct);
    Task<Result<bool>> SetActive(Guid actorId, Guid userId, bool active, CancellationToken ct);
}

public sealed class AccountHandlers(IAccountStore store) : IRequestHandler<ListAccountsQuery, Result<AccountPage>>,
    IRequestHandler<GetAccountQuery, Result<AccountDto>>, IRequestHandler<SetAccountStatusCommand, Result<bool>>
{
    public async Task<Result<AccountPage>> Handle(ListAccountsQuery request, CancellationToken ct) =>
        await store.List(request.ActorId, request.Page, request.PageSize, ct);

    public async Task<Result<AccountDto>> Handle(GetAccountQuery request, CancellationToken ct)
    {
        var account = await store.Get(request.ActorId, request.UserId, ct);
        return account;
    }

    public async Task<Result<bool>> Handle(SetAccountStatusCommand request, CancellationToken ct)
    {
        if (request.ActorId == request.UserId && !request.IsActive)
            return Result<bool>.Failure(new("iam.self_disable", "Bạn không thể tự khóa tài khoản của mình.", ErrorKind.Conflict));
        return await store.SetActive(request.ActorId, request.UserId, request.IsActive, ct);
    }
}

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    public ListAccountsValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000).WithMessage("Số trang phải từ 1 đến 1000000.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số tài khoản mỗi trang phải từ 1 đến 100.");
    }
}
