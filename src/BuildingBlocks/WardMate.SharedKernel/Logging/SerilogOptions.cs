namespace WardMate.SharedKernel.Logging;

/// <summary>
/// Configuration options for the WardMate centralised Serilog setup.
/// Bound from the "Serilog" section in appsettings.json.
/// </summary>
public sealed class SerilogOptions
{
    public const string SectionName = "Serilog";

    /// <summary>Minimum log level (Verbose/Debug/Information/Warning/Error/Fatal). Default: Information.</summary>
    public string MinimumLevel { get; init; } = "Information";

    /// <summary>When true, JSON-structured logs are written to the Console sink.</summary>
    public bool EnableConsoleSink { get; init; } = true;

    /// <summary>When true, rolling file logs are written to the path specified in <see cref="LogFilePath"/>.</summary>
    public bool EnableFileSink { get; init; } = true;

    /// <summary>Path template for rolling file log. {Date} token is supported by Serilog.Sinks.File.</summary>
    public string LogFilePath { get; init; } = "logs/wardmate-.log";

    /// <summary>Maximum size in MB of a single log file before rolling. Default 50 MB.</summary>
    public int FileSizeLimitMb { get; init; } = 50;

    /// <summary>Number of retained rolling log files. Default 7 (one week).</summary>
    public int RetainedFileCountLimit { get; init; } = 7;

    /// <summary>Name of the service/application written to every log event via the "Application" enricher.</summary>
    public string ApplicationName { get; init; } = "WardMate";
}
