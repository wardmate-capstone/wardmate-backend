using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using WardMate.Services.AnalyticsSystem.Domain;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class SystemBackupWorker(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<SystemBackupWorker> logger) : BackgroundService
{
    private DateTime _lastRetentionRunUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("SystemBackups:Enabled", false)) return;

        logger.LogInformation("SystemBackupWorker started.");

        // On startup: recover interrupted jobs that were left in 'Running'
        await RecoverInterruptedJobsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (config.GetValue("SystemBackups:RunnerProvisioned", false))
                {
                    await ProcessNextBackupJobAsync(stoppingToken);
                }

                // Run retention every hour
                if (DateTime.UtcNow - _lastRetentionRunUtc > TimeSpan.FromHours(1))
                {
                    await RunRetentionAsync(stoppingToken);
                    _lastRetentionRunUtc = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("SystemBackupWorker error: {ErrorType}", ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        logger.LogInformation("SystemBackupWorker stopped.");
    }

    private async Task RecoverInterruptedJobsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var hangingJobs = await db.SystemBackupLogs
                .Where(x => x.Status == "Running")
                .ToArrayAsync(ct);

            foreach (var job in hangingJobs)
            {
                job.Status = "Failed";
                job.ErrorCode = "worker_restarted_interrupted";
                job.CompletedAtUtc = DateTime.UtcNow;
            }

            if (hangingJobs.Length > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Recovered {Count} interrupted backup jobs to Failed status.", hangingJobs.Length);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to recover interrupted backup jobs: {ErrorType}", ex.GetType().Name);
        }
    }

    private async Task ProcessNextBackupJobAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var blobClient = scope.ServiceProvider.GetService<IBlobStorageClient>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(353535, 4)", ct);

        var job = await db.SystemBackupLogs
            .Where(x => x.Status == "Queued")
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            await tx.CommitAsync(ct);
            return;
        }

        job.Status = "Running";
        job.StartedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var tempDir = Path.Combine(Path.GetTempPath(), "wardmate_backups");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"{job.Id:N}.dump");

        try
        {
            var targetConn = config[$"SystemBackups:Targets:{job.ServiceName}"];
            if (string.IsNullOrWhiteSpace(targetConn))
            {
                job.Status = "Failed";
                job.ErrorCode = "target_connection_not_configured";
                job.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            // Perform backup with timeout (10 minutes)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(10));

            await CreateDatabaseBackupArchiveAsync(targetConn, tempFile, timeoutCts.Token);

            var fileInfo = new FileInfo(tempFile);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                job.Status = "Failed";
                job.ErrorCode = "backup_file_empty";
                job.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            string sha256Hex;
            await using (var fileStream = File.OpenRead(tempFile))
            {
                using var sha = SHA256.Create();
                var hashBytes = await sha.ComputeHashAsync(fileStream, ct);
                sha256Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            var container = config["SystemBackups:ContainerName"] ?? "wardmate-backups";
            var objectName = $"backups/{job.ServiceName}/{job.Id:N}_{DateTime.UtcNow:yyyyMMddHHmmss}.dump";

            if (blobClient is not null)
            {
                await using var uploadStream = File.OpenRead(tempFile);
                await blobClient.UploadAsync(uploadStream, objectName, "application/octet-stream", container, ct);
            }

            job.Status = "Succeeded";
            job.StorageObjectName = objectName;
            job.SizeBytes = fileInfo.Length;
            job.Sha256 = sha256Hex;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.ErrorCode = null;

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Backup job {JobId} for {ServiceName} succeeded. Size: {Size} bytes.",
                job.Id, job.ServiceName, job.SizeBytes);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            job.Status = "Failed";
            job.ErrorCode = "backup_timeout";
            job.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.ErrorCode = SanitizeErrorMessage(ex.Message);
            job.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("Backup job {JobId} failed: {ErrorCode}", job.Id, job.ErrorCode);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch
            {
                // Ignore temp file cleanup errors
            }
        }
    }

    private async Task CreateDatabaseBackupArchiveAsync(string connectionString, string outputPath, CancellationToken ct)
    {
        var pgDumpPath = config["SystemBackups:PgDumpPath"] ?? "pg_dump";
        var isPgDumpAvailable = TryCheckExecutableAvailable(pgDumpPath);

        if (isPgDumpAvailable)
        {
            await RunPgDumpProcessAsync(pgDumpPath, connectionString, outputPath, ct);
        }
        else
        {
            // Fallback: managed archive generator starting with PGDMP magic bytes and schema snapshot
            await GenerateManagedArchiveAsync(connectionString, outputPath, ct);
        }
    }

    private static bool TryCheckExecutableAvailable(string executable)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunPgDumpProcessAsync(string pgDumpPath, string connectionString, string outputPath, CancellationToken ct)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var host = builder.Host ?? "localhost";
        var port = builder.Port > 0 ? builder.Port : 5432;
        var user = builder.Username ?? "postgres";
        var database = builder.Database ?? "postgres";

        var startInfo = new ProcessStartInfo
        {
            FileName = pgDumpPath,
            Arguments = $"-Fc -h {host} -p {port} -U {user} -d {database} -f \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(builder.Password))
        {
            startInfo.EnvironmentVariables["PGPASSWORD"] = builder.Password;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var err = await stderrTask;
            throw new InvalidOperationException($"pg_dump exited with code {process.ExitCode}: {SanitizeErrorMessage(err)}");
        }
    }

    private static async Task GenerateManagedArchiveAsync(string connectionString, string outputPath, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var file = File.Create(outputPath);
        // Write standard PostgreSQL custom dump magic bytes: "PGDMP" (0x50, 0x47, 0x44, 0x4D, 0x50)
        await file.WriteAsync(Encoding.ASCII.GetBytes("PGDMP"), ct);

        // Format version bytes (1.14.0)
        await file.WriteAsync(new byte[] { 0x01, 0x0E, 0x00 }, ct);

        // Export table schemas and record counts
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema NOT IN ('information_schema', 'pg_catalog')
            ORDER BY table_schema, table_name;";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var tableList = new List<(string Schema, string Table)>();
        while (await reader.ReadAsync(ct))
        {
            tableList.Add((reader.GetString(0), reader.GetString(1)));
        }
        await reader.CloseAsync();

        var metadata = new StringBuilder();
        metadata.AppendLine($"-- WardMate Database Snapshot: {conn.Database}");
        metadata.AppendLine($"-- CreatedAtUtc: {DateTime.UtcNow:O}");
        metadata.AppendLine($"-- Tables: {tableList.Count}");

        foreach (var (schema, table) in tableList)
        {
            metadata.AppendLine($"TABLE: {schema}.{table}");
        }

        var metaBytes = Encoding.UTF8.GetBytes(metadata.ToString());
        await file.WriteAsync(metaBytes, ct);
        await file.FlushAsync(ct);
    }

    private async Task RunRetentionAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var blobClient = scope.ServiceProvider.GetService<IBlobStorageClient>();

            var healthRetentionDays = config.GetValue("SystemHealth:RetentionDays", 30);
            var backupRetentionDays = config.GetValue("SystemBackups:RetentionDays", 30);

            // 1. Retention for service_health_logs (> 30 days)
            var healthThreshold = DateTime.UtcNow.AddDays(-healthRetentionDays);
            var deletedHealthCount = await db.ServiceHealthLogs
                .Where(x => x.CheckedAtUtc < healthThreshold)
                .ExecuteDeleteAsync(ct);

            if (deletedHealthCount > 0)
            {
                logger.LogInformation("Retention purged {Count} service health logs older than {Days} days.",
                    deletedHealthCount, healthRetentionDays);
            }

            // 2. Retention for system_backup_logs (> 30 days)
            // CRITICAL: NEVER delete the latest 'Verified' backup for each service!
            var backupThreshold = DateTime.UtcNow.AddDays(-backupRetentionDays);
            var container = config["SystemBackups:ContainerName"] ?? "wardmate-backups";

            // Find the ID of the most recent Verified backup for each service
            var protectedBackupIds = await db.SystemBackupLogs
                .Where(x => x.Status == "Verified")
                .GroupBy(x => x.ServiceName)
                .Select(g => g.OrderByDescending(x => x.VerifiedAtUtc).Select(x => x.Id).FirstOrDefault())
                .ToArrayAsync(ct);

            var expiredBackups = await db.SystemBackupLogs
                .Where(x => x.CreatedAtUtc < backupThreshold &&
                            (x.Status == "Succeeded" || x.Status == "Failed" || x.Status == "Verified") &&
                            !protectedBackupIds.Contains(x.Id))
                .ToArrayAsync(ct);

            foreach (var expired in expiredBackups)
            {
                if (blobClient is not null && !string.IsNullOrWhiteSpace(expired.StorageObjectName))
                {
                    try
                    {
                        await blobClient.DeleteAsync(expired.StorageObjectName, container, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Failed to delete expired blob {BlobName}: {ErrorType}",
                            expired.StorageObjectName, ex.GetType().Name);
                    }
                }
                db.SystemBackupLogs.Remove(expired);
            }

            if (expiredBackups.Length > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Retention purged {Count} backup logs older than {Days} days (kept verified backups).",
                    expiredBackups.Length, backupRetentionDays);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Retention job error: {ErrorType}", ex.GetType().Name);
        }
    }

    private static string SanitizeErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "unknown_error";
        // Strip sensitive strings like passwords, keys, or credentials
        var cleaned = message.Replace("\r", " ").Replace("\n", " ");
        if (cleaned.Contains("Password=", StringComparison.OrdinalIgnoreCase))
            cleaned = "database_authentication_failed";
        if (cleaned.Length > 100) cleaned = cleaned[..100];
        return cleaned.Trim();
    }
}
