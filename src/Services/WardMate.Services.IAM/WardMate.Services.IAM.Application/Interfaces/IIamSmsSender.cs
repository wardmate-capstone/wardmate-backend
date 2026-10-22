using WardMate.Services.IAM.Application.Common;
namespace WardMate.Services.IAM.Application.Interfaces;
public interface IIamSmsSender
{
    bool Enabled { get; }
    Task<Result<bool>> SendOtp(string phoneNumber, string otpCode, CancellationToken ct);
}
