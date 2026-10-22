using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.Services.IAM.Infrastructure.Persistence;
namespace WardMate.Services.IAM.Infrastructure.Security;
public sealed class AccountSecurityCleanupWorker(IServiceScopeFactory scopes, IConfiguration config, TimeProvider clock, ILogger<AccountSecurityCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
                var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(config.GetValue("AccountSecurity:RetentionDays", 30), 2, 365));
                // Bounded batches; retain at least 48 hours so rolling OTP quotas cannot be reset by cleanup.
                var otpIds = await db.UserOtps.Where(x => x.ExpiresAtUtc < cutoff).OrderBy(x => x.ExpiresAtUtc).Select(x => x.Id).Take(1000).ToArrayAsync(ct);
                await db.UserOtps.Where(x => otpIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
                var tokenIds = await db.RefreshTokens.Where(x => x.ExpiresAt < cutoff).OrderBy(x => x.ExpiresAt).Select(x => x.Id).Take(1000).ToArrayAsync(ct);
                await db.RefreshTokens.Where(x => tokenIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
                var mailIds = await db.WelcomeEmails.Where(x => x.SentAtUtc < cutoff).OrderBy(x => x.SentAtUtc).Select(x => x.Id).Take(1000).ToArrayAsync(ct);
                await db.WelcomeEmails.Where(x => mailIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Account security cleanup failed: {FailureType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromHours(1), ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
