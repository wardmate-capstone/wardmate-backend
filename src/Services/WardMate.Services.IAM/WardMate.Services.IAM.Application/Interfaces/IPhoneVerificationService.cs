using WardMate.Services.IAM.Application.Common;
namespace WardMate.Services.IAM.Application.Interfaces;
public sealed record PhoneVerificationStatus(bool Verified, string? MaskedPhoneNumber);
public interface IPhoneVerificationService
{
    Task<Result<PhoneVerificationStatus>> Status(Guid userId, CancellationToken ct);
    Task<Result<string>> Request(Guid userId, string phoneNumber, string currentPassword, CancellationToken ct);
    Task<Result<string>> Verify(Guid userId, string otp, CancellationToken ct);
}
