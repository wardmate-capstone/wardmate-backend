using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Commands;

public sealed class RegisterCitizenHandler(IIdentityStore store, IPasswordHasher passwords, TimeProvider clock)
    : IRequestHandler<RegisterCitizenCommand, Result<CurrentUserDto>>
{
    public async Task<Result<CurrentUserDto>> Handle(RegisterCitizenCommand request, CancellationToken ct)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await store.AccountExists(username, email, ct)) return Result<CurrentUserDto>.Failure(AuthErrors.DuplicateAccount);
        var now = clock.GetUtcNow().UtcDateTime;
        var role = await store.GetCitizenRole(ct);
        var user = new User { Username = username, Email = email, PasswordHash = passwords.Hash(request.Password), CreatedAt = now, UpdatedAt = now };
        user.Profile = new UserProfile { UserId = user.Id, FullName = request.FullName.Trim(), UpdatedAt = now };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        store.AddUser(user);
        var outcome = await store.SaveChanges(ct);
        return outcome == SaveOutcome.Saved
            ? Result<CurrentUserDto>.Success(CurrentUserDto.From(user))
            : Result<CurrentUserDto>.Failure(AuthErrors.DuplicateAccount);
    }
}
