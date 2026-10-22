using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Persistence;
namespace WardMate.Services.IAM.Infrastructure.Security;
public sealed class PhoneVerificationService(IamDbContext db, IIamSmsSender sms, IPasswordHasher passwords, IConfiguration config, TimeProvider clock) : IPhoneVerificationService
{
    public async Task<Result<PhoneVerificationStatus>> Status(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(x => x.Profile).SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive) return Result<PhoneVerificationStatus>.Failure(AuthErrors.UserUnavailable);
        var phone = IamSmsSender.NormalizePhone(user.Profile?.PhoneNumber ?? "");
        return Result<PhoneVerificationStatus>.Success(new(phone is not null && phone == user.VerifiedPhoneNumber,
            phone is null ? null : phone[..3] + "****" + phone[^3..]));
    }
    private static Result<string> Error(string code, string message) => Result<string>.Failure(new(code, message, ErrorKind.BadRequest));
    private byte[] Hash(UserOtp row, string code)
    {
        var key = config["PasswordOtp:HashKey"] ?? config["Jwt:Key"] ?? "";
        if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("OTP hash key is not configured.");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes($"{row.Id:N}:{row.UserId:N}:{row.Purpose}:{row.TargetPhoneNumber}:{code}"));
    }
    public async Task<Result<string>> Request(Guid userId, string phoneNumber, string currentPassword, CancellationToken ct)
    {
        var phone = IamSmsSender.NormalizePhone(phoneNumber ?? "");
        if (phone is null) return Error("iam.phone_invalid", "Số di động Việt Nam không hợp lệ.");
        if (!sms.Enabled || config["Sms:Sandbox"] != "0") return Error("iam.sms_unavailable", "Xác minh số điện thoại cần SMS thật; Sandbox không xác minh quyền sở hữu số.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is null || !user.IsActive) return Result<string>.Failure(AuthErrors.UserUnavailable);
        if (string.IsNullOrEmpty(currentPassword) || Encoding.UTF8.GetByteCount(currentPassword) > 72 || !passwords.Verify(currentPassword, user.PasswordHash)) return Result<string>.Failure(AuthErrors.InvalidCredentials);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await OtpLimits.Exceeded(db, config, userId, "SMS", now, ct) || await db.UserOtps.AnyAsync(x => x.UserId == userId && x.CreatedAtUtc > now.AddSeconds(-60), ct))
            return Error("iam.otp_limit", "Đã đạt giới hạn OTP hoặc chưa đủ 60 giây. Vui lòng thử lại sau.");
        await db.UserOtps.Where(x => x.UserId == userId && !x.IsUsed && x.ExpiresAtUtc > now).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, now), ct);
        var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var row = new UserOtp { UserId = userId, Channel = "SMS", Purpose = "VERIFY_PHONE", TargetPhoneNumber = phone, CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(5) };
        row.OtpHash = Convert.ToHexString(Hash(row, code)); db.UserOtps.Add(row); await db.SaveChangesAsync(ct);
        var result = await sms.SendOtp(phone, code, ct);
        if (!result.IsSuccess) { row.ExpiresAtUtc = now; await db.SaveChangesAsync(ct); }
        await tx.CommitAsync(ct);
        return result.IsSuccess ? Result<string>.Success("Đã yêu cầu gửi mã xác minh số điện thoại; mã có hiệu lực 5 phút.") : Result<string>.Failure(result.Error!);
    }
    public async Task<Result<string>> Verify(Guid userId, string otp, CancellationToken ct)
    {
        if (!Regex.IsMatch(otp ?? "", "^[0-9]{6}$")) return Error("iam.otp_invalid", "OTP không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is null || !user.IsActive) return Result<string>.Failure(AuthErrors.UserUnavailable);
        var now = clock.GetUtcNow().UtcDateTime;
        var row = await db.UserOtps.Where(x => x.UserId == userId && x.Purpose == "VERIFY_PHONE").OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (row is null || row.IsUsed || row.Attempts >= 5 || row.ExpiresAtUtc <= now) return Error("iam.otp_invalid", "OTP hết hạn hoặc không hợp lệ.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(row.OtpHash), Hash(row, otp!)))
        { row.Attempts++; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Error("iam.otp_invalid", "OTP không hợp lệ."); }
        var profile = await db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null) return Error("iam.profile_required", "Cần tạo hồ sơ cá nhân trước khi xác minh số điện thoại.");
        profile.PhoneNumber = row.TargetPhoneNumber; profile.UpdatedAt = now; user.VerifiedPhoneNumber = row.TargetPhoneNumber;
        row.IsUsed = true; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result<string>.Success("Đã xác minh và cập nhật số điện thoại.");
    }
}
