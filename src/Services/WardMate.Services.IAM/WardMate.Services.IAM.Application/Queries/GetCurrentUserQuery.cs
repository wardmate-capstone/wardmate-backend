using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;

namespace WardMate.Services.IAM.Application.Queries;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<CurrentUserDto>>;
public sealed class GetCurrentUserHandler(IIdentityStore store) : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var user = await store.FindUser(request.UserId, ct);
        return user is null || !user.IsActive ? Result<CurrentUserDto>.Failure(AuthErrors.UserUnavailable) : Result<CurrentUserDto>.Success(CurrentUserDto.From(user));
    }
}
