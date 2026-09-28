using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;

namespace WardMate.Services.IAM.Application.Profiles;

public sealed record ProfileInput(string FullName, string? IdentityNumber, string? PhoneNumber,
    DateOnly? DateOfBirth, string? Gender, string? PermanentAddress, string? TemporaryAddress);
public sealed record GetProfileQuery(Guid UserId) : IRequest<Result<UserProfileDto>>;
public sealed record CreateProfileCommand(Guid UserId, ProfileInput Profile) : IRequest<Result<UserProfileDto>>;
public sealed record UpdateProfileCommand(Guid UserId, ProfileInput Profile) : IRequest<Result<UserProfileDto>>;
public sealed record DeleteProfileCommand(Guid UserId) : IRequest<Result<bool>>;

public static class ProfileErrors
{
    public static readonly Error NotFound = new("iam.profile_not_found", "Không tìm thấy hồ sơ người dùng.", ErrorKind.NotFound);
    public static readonly Error UserNotFound = new("iam.user_not_found", "Không tìm thấy tài khoản người dùng.", ErrorKind.NotFound);
    public static readonly Error Exists = new("iam.profile_exists", "Hồ sơ người dùng đã tồn tại.", ErrorKind.Conflict);
    public static readonly Error Duplicate = new("iam.profile_conflict", "Hồ sơ hoặc số giấy tờ định danh đã tồn tại.", ErrorKind.Conflict);
    public static readonly Error ConcurrentUpdate = new("iam.profile_changed", "Hồ sơ đã thay đổi. Vui lòng tải lại và thử lại.", ErrorKind.Conflict);
}
