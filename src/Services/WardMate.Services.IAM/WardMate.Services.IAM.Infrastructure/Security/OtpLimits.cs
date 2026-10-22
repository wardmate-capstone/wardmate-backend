using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.IAM.Infrastructure.Persistence;
namespace WardMate.Services.IAM.Infrastructure.Security;
internal static class OtpLimits
{
    internal static async Task<bool> Exceeded(IamDbContext db, IConfiguration config, Guid userId, string channel, DateTime now, CancellationToken ct)
    {
        var rows = db.UserOtps.Where(x => x.UserId == userId && x.CreatedAtUtc > now.AddHours(-24));
        if (await rows.CountAsync(ct) >= Math.Clamp(config.GetValue("PasswordOtp:MaxRequestsPer24Hours", 10), 1, 100)) return true;
        return channel == "SMS" && await rows.CountAsync(x => x.Channel == "SMS", ct) >= Math.Clamp(config.GetValue("PasswordOtp:MaxSmsPer24Hours", 5), 1, 20);
    }
}
