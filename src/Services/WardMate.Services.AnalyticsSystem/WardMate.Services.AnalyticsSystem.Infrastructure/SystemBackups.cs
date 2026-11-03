using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Domain;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class SystemBackups(AnalyticsDbContext db, IConfiguration config, IServiceProvider sp) : ISystemBackups
{
    private void Enabled()
    {
        if (!config.GetValue("SystemBackups:Enabled", false))
            throw new AnalyticsException(503, "system.backups_disabled", "Backup chưa được cấu hình và bật.");
    }
    public async Task<BackupSummary> Create(CreateBackupRequest request, Guid actor, CancellationToken ct)
    {
        Enabled();
        // Do not accept jobs until a runner has explicitly been provisioned.
        if (!config.GetValue("SystemBackups:RunnerProvisioned", false))
            throw new AnalyticsException(503, "system.backup_runner_unavailable", "Chưa có backup runner.");
        if (actor == Guid.Empty) throw new AnalyticsException(401, "auth.unauthorized", "Vui lòng đăng nhập.");
        if (sp.GetService<IBlobStorageClient>() is null)
            throw new AnalyticsException(503, "system.backup_storage_unavailable", "Chưa cấu hình Blob storage.");
        if (string.IsNullOrWhiteSpace(request.ServiceName) || request.ServiceName.Length > 64 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 500)
            throw new AnalyticsException(400, "system.invalid_backup", "Tên service và lý do không hợp lệ.");
        if (!config.GetSection("SystemBackups:Targets").GetChildren().Any(x => x.Key == request.ServiceName))
            throw new AnalyticsException(404, "system.backup_target_not_found", "Service chưa được cấu hình backup.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(353535, 3)", ct);
        if (await db.SystemBackupLogs.AnyAsync(x => x.ServiceName == request.ServiceName &&
            (x.Status == "Queued" || x.Status == "Running"), ct))
            throw new AnalyticsException(409, "system.backup_in_progress", "Service đã có backup đang chờ hoặc đang chạy.");
        var row = new SystemBackupLog { Id = Guid.NewGuid(), ServiceName = request.ServiceName,
            RequestedBy = actor, Reason = request.Reason.Trim(), CreatedAtUtc = DateTime.UtcNow };
        db.SystemBackupLogs.Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }
    public async Task<BackupSummary> Get(Guid id, CancellationToken ct)
    {
        Enabled();
        return Map(await db.SystemBackupLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AnalyticsException(404, "system.backup_not_found", "Không tìm thấy backup."));
    }
    public async Task<AnalyticsPage<BackupSummary>> List(string? service, string? status, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct)
    {
        Enabled();
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100 || from > to ||
            from is { Kind: not DateTimeKind.Utc } || to is { Kind: not DateTimeKind.Utc })
            throw new AnalyticsException(400, "system.invalid_query", "Dùng UTC có Z, from <= to và phân trang hợp lệ (page 1..100000, pageSize 1..100).");
        if (status is not null && status is not ("Queued" or "Running" or "Succeeded" or "Failed" or "Verified"))
            throw new AnalyticsException(400, "system.invalid_status", "Trạng thái hợp lệ: Queued, Running, Succeeded, Failed, Verified.");

        var query = db.SystemBackupLogs.AsNoTracking().AsQueryable();
        if (service is not null) query = query.Where(x => x.ServiceName == service);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (from.HasValue) query = query.Where(x => x.CreatedAtUtc >= from);
        if (to.HasValue) query = query.Where(x => x.CreatedAtUtc <= to);

        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(Map).ToArray(), page, pageSize, count);
    }
    public async Task<BackupSummary> Verify(Guid id, Guid actor, CancellationToken ct)
    {
        Enabled();
        if (actor == Guid.Empty) throw new AnalyticsException(401, "auth.unauthorized", "Vui lòng đăng nhập.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Retention uses the same lock so it cannot delete a file during verification.
        if (!await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock(353535, 5) AS \"Value\"").SingleAsync(ct))
            throw new AnalyticsException(409, "system.backup_verification_busy", "Đang xác minh hoặc dọn backup; hãy thử lại sau.");
        var row = await db.SystemBackupLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) throw new AnalyticsException(404, "system.backup_not_found", "Không tìm thấy backup.");
        if (row.Status is "Queued" or "Running")
            throw new AnalyticsException(409, "system.backup_in_progress", "Bản backup đang chờ hoặc đang chạy, chưa thể xác minh.");
        if (row.Status == "Failed")
            throw new AnalyticsException(400, "system.backup_failed", "Không thể xác minh bản backup đã thất bại.");
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dump");
        using var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        operationTimeout.CancelAfter(TimeSpan.FromMinutes(5));
        var operationToken = operationTimeout.Token;
        try
        {
            if (string.IsNullOrWhiteSpace(row.StorageObjectName))
                throw new AnalyticsException(400, "system.backup_missing_storage", "Bản backup không có file lưu trữ để xác minh.");
            var blobClient = sp.GetService<IBlobStorageClient>()
                ?? throw new AnalyticsException(503, "system.backup_storage_unavailable", "Chưa cấu hình Blob storage.");
            var container = config["SystemBackups:ContainerName"] ?? "wardmate-backups";
            await using var stream = await blobClient.DownloadAsync(row.StorageObjectName, container, operationToken);
            if (stream is null)
                throw new AnalyticsException(404, "system.backup_file_missing", "Không tìm thấy file backup.");
            var maximumBytes = config.GetValue<long>("SystemBackups:MaxVerifyBytes", 1073741824);
            if (maximumBytes < 1 || row.SizeBytes is null || row.SizeBytes <= 0 || row.SizeBytes > maximumBytes)
                throw new AnalyticsException(400, "system.backup_size_limit", "Kích thước backup vượt giới hạn xác minh hoặc thiếu metadata.");
            await using (var file = File.Create(temp))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, operationToken)) != 0)
                {
                    total += read;
                    if (total > row.SizeBytes || total > maximumBytes)
                        throw new AnalyticsException(400, "system.backup_size_mismatch", "Kích thước backup không khớp.");
                    await file.WriteAsync(buffer.AsMemory(0, read), operationToken);
                }
            }
            if (row.SizeBytes is null || new FileInfo(temp).Length != row.SizeBytes)
                throw new AnalyticsException(400, "system.backup_size_mismatch", "Kích thước backup không khớp.");
            await using (var file = File.OpenRead(temp))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, operationToken));
                if (!string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new AnalyticsException(400, "system.backup_checksum_mismatch", "Checksum không khớp.");
            }
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = config["SystemBackups:PgRestorePath"] ?? "pg_restore",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            // Decode the complete archive, including data blocks, without connecting to a database.
            info.ArgumentList.Add("--file=-");
            info.ArgumentList.Add(temp);
            using var process = new System.Diagnostics.Process { StartInfo = info };
            try { process.Start(); }
            catch (System.ComponentModel.Win32Exception)
            { throw new AnalyticsException(503, "system.backup_verifier_unavailable", "Cần cài pg_restore."); }
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, operationToken);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, operationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { if (!process.HasExited) process.Kill(true); throw; }
            await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0)
                throw new AnalyticsException(400, "system.backup_invalid_archive", "pg_restore không đọc được archive.");
        }
        catch (Exception ex)
        {
            var failure = ex as AnalyticsException ?? (ex is OperationCanceledException
                ? new AnalyticsException(503, "system.backup_verification_timeout", "Xác minh bị gián đoạn hoặc hết thời gian; hãy thử lại.")
                : new AnalyticsException(503, "system.backup_verification_unavailable", "Không thể đọc hoặc xác minh backup lúc này."));
            row.Status = "Succeeded";
            row.VerifiedAtUtc = null;
            row.ErrorCode = failure.Code;
            using var persistTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await db.SaveChangesAsync(persistTimeout.Token);
            await transaction.CommitAsync(persistTimeout.Token);
            throw failure;
        }
        finally { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

        row.VerifiedAtUtc = DateTime.UtcNow;
        row.Status = "Verified";
        row.ErrorCode = null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(row);
    }
    private static BackupSummary Map(SystemBackupLog x) => new(x.Id, x.ServiceName, x.Status,
        x.SizeBytes, x.Sha256, x.RequestedBy, x.Reason, x.CreatedAtUtc, x.StartedAtUtc,
        x.CompletedAtUtc, x.VerifiedAtUtc, x.RestoreTestedAtUtc, x.ErrorCode);
}
