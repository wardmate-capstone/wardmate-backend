using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class LeaveBlobMaintenance(IServiceScopeFactory scopes,IConfiguration config,ILogger<LeaveBlobMaintenance> log):BackgroundService
{
    internal const string Container="workflow-leave-documents";
    internal static async Task<bool> Private(BlobServiceClient client,CancellationToken ct) =>
        (await client.GetBlobContainerClient(Container).GetPropertiesAsync(cancellationToken:ct)).Value.PublicAccess==PublicAccessType.None;
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!config.GetValue<bool>("WorkSchedule:CleanupOrphans")) return;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();
                var client=scope.ServiceProvider.GetRequiredService<BlobServiceClient>();
                if(!await Private(client,ct)) throw new InvalidOperationException("private_container_required");
                var db=scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
                var container=client.GetBlobContainerClient(Container);
                var cutoff=DateTimeOffset.UtcNow.AddDays(-7);
                await foreach(var item in container.GetBlobsAsync(prefix:"drafts/",cancellationToken:ct))
                {
                    if(item.Properties.LastModified is not {} updated || updated>=cutoff || item.Properties.ETag is not {} etag) continue;
                    var parts=item.Name.Split('/');
                    if(parts.Length!=3 || !Guid.TryParseExact(parts[1],"N",out var draft) || !parts[2].EndsWith(".docx",StringComparison.Ordinal) ||
                        !Guid.TryParseExact(parts[2][..^5],"N",out var revision)) continue;
                    if(await db.LeaveRevisions.AsNoTracking().AnyAsync(x=>x.Id==revision && x.DraftId==draft,ct)) continue;
                    await container.GetBlobClient(item.Name).DeleteIfExistsAsync(conditions:new BlobRequestConditions {IfMatch=etag},cancellationToken:ct);
                }
            }
            catch(Exception e) when(!ct.IsCancellationRequested)
            { log.LogWarning("Không thể dọn Blob đơn nghỉ trong lượt này: {ErrorType}",e.GetType().Name); }
            await Task.Delay(TimeSpan.FromHours(24),ct);
        }
    }
}
