using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WardMate.Auditing;

public sealed class AuditConnectionInterceptor(IHttpContextAccessor accessor) : DbConnectionInterceptor
{
    private DbCommand Metadata(DbConnection connection)
    {
        var http = accessor.HttpContext;
        var actor = http?.User.Identity?.IsAuthenticated == true && Guid.TryParse(http.User.FindFirst("sub")?.Value, out var id)
            ? id.ToString() : "";
        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('wardmate.audit_actor',@actor,false),set_config('wardmate.audit_ip',@ip,false),set_config('wardmate.audit_trace',@trace,false)";
        foreach (var (name,value) in new[] { ("actor",actor), ("ip",http?.Connection.RemoteIpAddress?.ToString() ?? ""),
            ("trace",Activity.Current?.TraceId.ToString() ?? http?.TraceIdentifier ?? "") })
        { var p=command.CreateParameter(); p.ParameterName=name; p.Value=value; command.Parameters.Add(p); }
        return command;
    }
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    { using var command=Metadata(connection); command.ExecuteNonQuery(); }
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    { await using var command=Metadata(connection); await command.ExecuteNonQueryAsync(cancellationToken); }
}
