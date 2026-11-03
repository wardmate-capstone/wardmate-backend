using System.Security.Cryptography;
using System.Text;
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
        var row = await db.SystemBackupLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) throw new AnalyticsException(404, "system.backup_not_found", "Không tìm thấy backup.");
        if (row.Status is "Queued" or "Running")
            throw new AnalyticsException(409, "system.backup_in_progress", "Bản backup đang chờ hoặc đang chạy, chưa thể xác minh.");
        if (row.Status == "Failed")
            throw new AnalyticsException(400, "system.backup_failed", "Không thể xác minh bản backup đã thất bại.");
        if (string.IsNullOrWhiteSpace(row.StorageObjectName))
            throw new AnalyticsException(400, "system.backup_missing_storage", "Bản backup không có file lưu trữ để xác minh.");

        var blobClient = sp.GetService<IBlobStorageClient>();
        var container = config["SystemBackups:ContainerName"] ?? "wardmate-backups";
        if (blobClient is not null)
        {
            await using var stream = await blobClient.DownloadAsync(row.StorageObjectName, container, ct);
            if (stream is null)
            {
                row.ErrorCode = "blob_not_found";
                await db.SaveChangesAsync(ct);
                throw new AnalyticsException(404, "system.backup_file_missing", "File backup không tồn tại trên kho lưu trữ Blob.");
            }
            using var sha = SHA256.Create();
            var hashBytes = await sha.ComputeHashAsync(stream, ct);
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            if (!string.Equals(hashHex, row.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                row.ErrorCode = "checksum_mismatch";
                await db.SaveChangesAsync(ct);
                throw new AnalyticsException(400, "system.backup_checksum_mismatch", "Checksum không khớp; file backup có thể đã bị hỏng.");
            }
            if (stream.CanSeek)
            {
                stream.Position = 0;
                var header = new byte[5];
                var read = await stream.ReadAsync(header, 0, 5, ct);
                if (read == 5 && Encoding.ASCII.GetString(header) != "PGDMP")
                {
                    row.ErrorCode = "invalid_archive_format";
                    await db.SaveChangesAsync(ct);
                    throw new AnalyticsException(400, "system.backup_invalid_archive", "Định dạng file không phải archive hợp lệ.");
                }
            }
        }
        else if (File.Exists(row.StorageObjectName))
        {
            await using var fileStream = File.OpenRead(row.StorageObjectName);
            using var sha = SHA256.Create();
            var hashBytes = await sha.ComputeHashAsync(fileStream, ct);
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
            if (!string.Equals(hashHex, row.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                row.ErrorCode = "checksum_mismatch";
                await db.SaveChangesAsync(ct);
                throw new AnalyticsException(400, "system.backup_checksum_mismatch", "Checksum không khớp; file backup có thể đã bị hỏng.");
            }
        }
        else if (!config.GetValue("SystemBackups:RunnerProvisioned", false))
        {
            throw new AnalyticsException(503, "system.backup_runner_unavailable", "Chưa có backup runner hoặc kho lưu trữ Blob.");
        }

        row.VerifiedAtUtc = DateTime.UtcNow;
        row.Status = "Verified";
        row.ErrorCode = null;
        await db.SaveChangesAsync(ct);
        return Map(row);
    }
    private static BackupSummary Map(SystemBackupLog x) => new(x.Id, x.ServiceName, x.Status,
        x.SizeBytes, x.Sha256, x.RequestedBy, x.Reason, x.CreatedAtUtc, x.StartedAtUtc,
        x.CompletedAtUtc, x.VerifiedAtUtc, x.RestoreTestedAtUtc, x.ErrorCode);
}
