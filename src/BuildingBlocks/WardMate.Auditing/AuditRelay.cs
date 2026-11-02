using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WardMate.Auditing;

public sealed class AuditRelay<T>(IServiceScopeFactory scopes, IHttpClientFactory clients, AuditRelayOptions<T> options,
    ILogger<AuditRelay<T>> logger) : BackgroundService where T:DbContext
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope=scopes.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<T>();
        if((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            throw new InvalidOperationException("Apply source database migrations before enabling Audit:RelayEnabled.");
        await base.StartAsync(cancellationToken);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DeliverOne(stoppingToken); }
            catch (OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Audit relay {Source} unavailable ({Type}); committed events remain queued",options.Source,ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
        }
    }
    public async Task<bool> DeliverOne(CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<T>();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await using var select=db.Database.GetDbConnection().CreateCommand();
        select.Transaction=tx.GetDbTransaction();
        select.CommandText="SELECT id,payload::text FROM audit_transport.outbox WHERE source=@source AND delivered_at IS NULL AND next_attempt_at<=now() ORDER BY occurred_at,id LIMIT 1 FOR UPDATE SKIP LOCKED";
        Parameter(select,"source",options.Source);
        Guid id; string body;
        await using(var reader=await select.ExecuteReaderAsync(ct))
        { if (!await reader.ReadAsync(ct)) return false; id=reader.GetGuid(0); body=reader.GetString(1); }
        var timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var request=new HttpRequestMessage(HttpMethod.Post,options.Url);
        request.Content=new StringContent(body,Encoding.UTF8,"application/json");
        request.Headers.Add("X-Audit-Source",options.Source);
        request.Headers.Add("X-Audit-Time",timestamp);
        request.Headers.Add("X-Audit-Signature",AuditSignature.Sign(options.Key,options.Source,timestamp,body));
        var success=false; var error="transport";
        try
        {
            using var response=await clients.CreateClient("audit-relay").SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            success=response.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.NoContent;
            error="http_"+((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        }
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException && !ct.IsCancellationRequested) { }
        await using var update=db.Database.GetDbConnection().CreateCommand();
        update.Transaction=tx.GetDbTransaction();
        update.CommandText=success
            ? "UPDATE audit_transport.outbox SET delivered_at=now(),last_error=NULL WHERE id=@id"
            : "UPDATE audit_transport.outbox SET attempts=least(attempts+1,1000000),last_error=@error,next_attempt_at=now()+interval '60 seconds' WHERE id=@id";
        Parameter(update,"id",id); if(!success) Parameter(update,"error",error);
        await update.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
        return success;
    }
    private static void Parameter(DbCommand command,string name,object value)
    { var p=command.CreateParameter(); p.ParameterName=name; p.Value=value; command.Parameters.Add(p); }
}
