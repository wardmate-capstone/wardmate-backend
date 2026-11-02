using System.Diagnostics;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API;

// Request metadata only. A business service writes the audit in its own DB transaction.
// Never read forwarded headers, bearer tokens or request bodies here.
public sealed class SystemAuditContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Items[typeof(SystemAuditRequest)] = new SystemAuditRequest(
            context.Connection.RemoteIpAddress?.ToString(), Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
        await next(context);
    }
}

public sealed record SystemAuditRequest(string? ClientIp, string TraceId);
