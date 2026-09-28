using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace WardMate.SharedKernel.Logging;

/// <summary>
/// Extension methods for configuring WardMate centralised Serilog structured logging.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Configures Serilog as the logging provider for the host.
    /// Call this on <see cref="WebApplicationBuilder"/> before <c>builder.Build()</c>.
    /// </summary>
    /// <remarks>
    /// Reads settings from the "Serilog" section in appsettings.json.
    /// Falls back to sensible defaults when the section is absent.
    /// </remarks>
    public static WebApplicationBuilder AddWardMateLogging(this WebApplicationBuilder builder)
    {
        var opts = builder.Configuration
            .GetSection(SerilogOptions.SectionName)
            .Get<SerilogOptions>() ?? new SerilogOptions();

        var minimumLevel = Enum.TryParse<LogEventLevel>(opts.MinimumLevel, ignoreCase: true, out var parsed)
            ? parsed
            : LogEventLevel.Information;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            // Suppress noisy framework namespaces
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            // Enrichers
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithProperty("Application", opts.ApplicationName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

        // Console sink — JSON in Production, human-readable in Development
        if (opts.EnableConsoleSink)
        {
            if (builder.Environment.IsDevelopment())
                loggerConfig.WriteTo.Console(minimumLevel,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
            else
                loggerConfig.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
        }

        // Rolling file sink
        if (opts.EnableFileSink)
        {
            loggerConfig.WriteTo.File(
                path: opts.LogFilePath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: (long)opts.FileSizeLimitMb * 1024 * 1024,
                retainedFileCountLimit: opts.RetainedFileCountLimit,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfig.CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    /// <summary>
    /// Adds the Serilog HTTP request logging middleware.
    /// Call this after <c>app.Build()</c>, before routing middleware.
    /// </summary>
    public static IApplicationBuilder UseWardMateRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("ClientIp",
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            };
        });
        return app;
    }
}
