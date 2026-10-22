using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Infrastructure.Persistence;
namespace WardMate.Services.IAM.Infrastructure.Security;

public sealed class WelcomeEmailWorker(IServiceScopeFactory scopes, ILogger<WelcomeEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<IIamEmailSender>();
                if (!sender.Enabled) { await Task.Delay(5000, ct); continue; }
                var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var row = await db.WelcomeEmails.FromSqlRaw("SELECT * FROM iam.welcome_emails WHERE sent_at_utc IS NULL AND attempts < 10 AND next_attempt_at_utc <= now() ORDER BY next_attempt_at_utc LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
                if (row is null) { await tx.RollbackAsync(ct); await Task.Delay(1000, ct); continue; }
                var user = await db.Users.Include(x => x.Profile).SingleAsync(x => x.Id == row.UserId, ct);
                try
                {
                    if (!user.IsActive) row.Attempts = 10;
                    else { await sender.Welcome(user.Email, user.Profile?.FullName ?? user.Username, user.Username, ct); row.SentAtUtc = DateTime.UtcNow; }
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    row.Attempts++; row.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, row.Attempts)));
                    logger.LogWarning("IAM welcome delivery failed; attempt {Attempt}.", row.Attempts);
                }
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception)
            {
                logger.LogWarning("IAM welcome queue unavailable; retrying without logging recipient or credentials.");
                try { await Task.Delay(5000, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }
    }
}
