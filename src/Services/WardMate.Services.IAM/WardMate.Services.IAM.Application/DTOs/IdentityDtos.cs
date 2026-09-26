using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.DTOs;

public sealed record AuthResponseDto(string AccessToken, DateTime AccessTokenExpiresAt,
    string RefreshToken, DateTime RefreshTokenExpiresAt, string TokenType = "Bearer");
public sealed record UserProfileDto(string FullName, string? IdentityNumber, string? PhoneNumber,
    DateOnly? DateOfBirth, string? Gender, string? PermanentAddress, string? TemporaryAddress)
{
    public static UserProfileDto From(UserProfile profile) => new(profile.FullName, profile.IdentityNumber,
        profile.PhoneNumber, profile.DateOfBirth, profile.Gender, profile.PermanentAddress, profile.TemporaryAddress);
}
public sealed record CurrentUserDto(Guid Id, string Username, string Email, UserProfileDto? Profile,
    string[] Roles, string[] Permissions)
{
    public static CurrentUserDto From(User user) => new(user.Id, user.Username, user.Email,
        user.Profile is null ? null : UserProfileDto.From(user.Profile),
        user.UserRoles.Select(x => x.Role.RoleName).Distinct().Order().ToArray(),
        user.UserRoles.SelectMany(x => x.Role.RolePermissions).Select(x => x.Permission.PermissionCode).Distinct().Order().ToArray());
}
