using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace WardMate.SharedKernel.Logging;

/// <summary>
/// Custom ASP.NET Core middleware that logs detailed HTTP request/response metadata
/// including method, path, status code, elapsed time, and client IP.
/// This middleware supplements Serilog's built-in UseSerilogRequestLogging with
/// additional structured properties for the WardMate audit trail.
/// </summary>
public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    private static readonly string[] _healthCheckPaths = ["/health", "/", "/favicon.ico"];

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip health-check and favicon noise
        if (_healthCheckPaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var level = context.Response.StatusCode >= 500
                ? LogLevel.Error
                : context.Response.StatusCode >= 400
                    ? LogLevel.Warning
                    : LogLevel.Information;

            logger.Log(level,
                "HTTP {Method} {Path} {QueryString} => {StatusCode} [{ElapsedMs}ms] | IP: {ClientIp} | TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.TraceIdentifier);
        }
    }
}

/// <summary>Extension to register <see cref="RequestLoggingMiddleware"/> in the pipeline.</summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseWardMateRequestLoggingMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
