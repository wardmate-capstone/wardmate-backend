using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Rbac;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class CategoryReconciliationWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<CategoryReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue("CategoryReconciliation:Enabled", true)) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
                var rbac = scope.ServiceProvider.GetRequiredService<IRbacStore>();
                await rbac.Exclusive(async () =>
                {
                    // Read under the same IAM lock as assignments; never turn a failed read into an empty set.
                    var valid = await scope.ServiceProvider.GetRequiredService<ICategoryDirectory>().ReadIds(ct);
                    var users = await db.Users.Where(x => x.AssignedCategories.Any(id => !valid.Contains(id)))
                        .OrderBy(x => x.Id).Take(100).ToArrayAsync(ct);
                    foreach (var user in users)
                    {
                        var before = user.AssignedCategories.ToArray();
                        var after = before.Where(valid.Contains).Distinct().Order().ToArray();
                        user.AssignedCategories = after; user.UpdatedAt = DateTime.UtcNow;
                        rbac.Audit(Guid.Empty, "OFFICER_CATEGORIES_RECONCILED", user.Id, null, null,
                            JsonSerializer.Serialize(new { source = "CATALOG_RECONCILIATION", before, after }));
                    }
                    return Result<bool>.Success(true);
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Category reconciliation failed; assignments retained for retry."); }
            try { await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(config.GetValue("CategoryReconciliation:IntervalSeconds", 300), 30, 86400)), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
