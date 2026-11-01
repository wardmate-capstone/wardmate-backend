using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Persistence;
namespace WardMate.Services.IAM.Infrastructure.Security;

public sealed class PasswordOtpService(IamDbContext db, IIamEmailSender email, IPasswordHasher passwords,
    IConfiguration config, TimeProvider clock, ILogger<PasswordOtpService> logger) : IPasswordOtpService
{
    private static Result<string> Accepted() => Result<string>.Success("Nếu tài khoản hợp lệ và đủ thời gian gửi lại, mã OTP sẽ được gửi qua Email. Vui lòng kiểm tra và đợi ít nhất 60 giây trước khi yêu cầu lại.");
    private static Result<string> Invalid() => Result<string>.Failure(new("iam.otp_invalid", "OTP không hợp lệ, đã dùng, quá số lần thử hoặc hết hạn. Vui lòng yêu cầu mã mới.", ErrorKind.BadRequest));
    private byte[] Hash(UserOtp row, string code)
    {
        var key = config["PasswordOtp:HashKey"] ?? config["Jwt:Key"] ?? "";
        if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("OTP hash key is not configured.");
        // Per-challenge salt and server-side key prevent offline enumeration of just 1 million codes from a DB leak.
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes($"{row.Id:N}:{row.UserId:N}:{row.Purpose}:{code}"));
    }
    private Task<User?> Lock(string identity, CancellationToken ct) => db.Users
        .FromSqlInterpolated($"SELECT * FROM users WHERE (username = {identity} OR email = {identity}) FOR UPDATE")
        .AsNoTracking().SingleOrDefaultAsync(ct);
    private static bool Allowed(User? user, string purpose, Guid? actor) => user is { IsActive: true }
        && (purpose == "FORGOT_PASSWORD" || actor == user.Id);
    public async Task<Result<string>> Request(RequestPasswordOtpCommand r, CancellationToken ct)
    {
        if (r.Purpose == "CHANGE_PASSWORD" && r.ActorId is null) return Result<string>.Failure(AuthErrors.UserUnavailable);
        if (!email.Enabled) return Result<string>.Failure(new("iam.email_unavailable", "Dịch vụ Email chưa sẵn sàng. Vui lòng thử lại sau.", ErrorKind.ServiceUnavailable));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await Lock(r.EmailOrUsername.Trim().ToLowerInvariant(), ct);
        if (!Allowed(user, r.Purpose, r.ActorId)) return Accepted();
        var now = clock.GetUtcNow().UtcDateTime;
        if (await OtpLimits.Exceeded(db, config, user!.Id, now, ct)) return Accepted();
        if (await db.UserOtps.AnyAsync(x => x.UserId == user!.Id && x.CreatedAtUtc > now.AddSeconds(-60), ct)) return Accepted();
        await db.UserOtps.Where(x => x.UserId == user!.Id && !x.IsUsed && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, now), ct);
        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var otp = new UserOtp { UserId = user!.Id, Channel = "EMAIL", Purpose = r.Purpose, CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(5) };
        otp.OtpHash = Convert.ToHexString(Hash(otp, code)); db.UserOtps.Add(otp);
        await db.SaveChangesAsync(ct);
        try
        {
            await email.Otp(user.Email, code, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Do not reveal account existence or persist the plaintext OTP for retry.
            otp.ExpiresAtUtc = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            logger.LogWarning("IAM OTP delivery failed; challenge invalidated. No OTP or recipient is logged.");
        }
        await tx.CommitAsync(ct);
        return Accepted();
    }
    public async Task<Result<string>> Verify(VerifyPasswordOtpCommand r, CancellationToken ct)
    {
        if (r.Purpose == "CHANGE_PASSWORD" && r.ActorId is null) return Result<string>.Failure(AuthErrors.UserUnavailable);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await Lock(r.EmailOrUsername.Trim().ToLowerInvariant(), ct);
        if (!Allowed(user, r.Purpose, r.ActorId)) return Invalid();
        var now = clock.GetUtcNow().UtcDateTime;
        var otp = await db.UserOtps.Where(x => x.UserId == user!.Id && x.Purpose == r.Purpose)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (otp is null || otp.IsUsed || otp.Attempts >= 5 || otp.ExpiresAtUtc <= now) return Invalid();
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(otp.OtpHash), Hash(otp, r.Otp)))
        {
            otp.Attempts++; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Invalid();
        }
        var newHash = passwords.Hash(r.NewPassword);
        await db.Users.Where(x => x.Id == user!.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, newHash)
            .SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.SecurityStamp, Guid.NewGuid()), ct);
        await db.RefreshTokens.Where(x => x.UserId == user!.Id && !x.IsRevoked).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRevoked, true), ct);
        await db.UserOtps.Where(x => x.UserId == user!.Id && !x.IsUsed && x.Id != otp.Id && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, now), ct);
        otp.IsUsed = true; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result<string>.Success("Đổi mật khẩu thành công. Vui lòng đăng nhập lại.");
    }
}
