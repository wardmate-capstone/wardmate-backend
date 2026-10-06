using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;

namespace WardMate.Services.IAM.Application.Accounts;

public interface IManagementScope
{
    Task<bool> CanManage(Guid actorId, CancellationToken ct);
    Task<bool> CanAssignFrontDesk(Guid actorId, Guid userId, CancellationToken ct);
    Task<Result<T>> Execute<T>(Guid actorId, Guid userId, Func<Task<Result<T>>> operation, CancellationToken ct);
}

public sealed record ManagedRoleDto(int Id, string RoleName);
public sealed record ManagedUserDto(Guid Id, string Username, string Email, bool IsActive,
    Guid? WardId, string? WardName, UserProfileDto? Profile, ManagedRoleDto[] Roles);
public sealed record ManagedUserPage(ManagedUserDto[] Items, int Page, int PageSize, int Total);
public sealed record WardDto(Guid Id, string Code, string Name);

public interface IStaffAdministration
{
    Task<Result<ManagedUserPage>> ListUsers(Guid actorId, int page, int pageSize, CancellationToken ct);
    Task<Result<WardDto[]>> ListWards(Guid actorId, CancellationToken ct);
    Task<Result<WardDto>> CreateWard(Guid actorId, string code, string name, CancellationToken ct);
    Task<Result<bool>> AssignWard(Guid actorId, Guid userId, Guid? wardId, CancellationToken ct);
    Task<Result<ManagedUserDto>> CreateFrontDesk(Guid actorId, CreateFrontDeskInput input, CancellationToken ct);
}

public sealed record CreateFrontDeskInput(string Username, string Email, string Password, string FullName, Guid? WardId);
