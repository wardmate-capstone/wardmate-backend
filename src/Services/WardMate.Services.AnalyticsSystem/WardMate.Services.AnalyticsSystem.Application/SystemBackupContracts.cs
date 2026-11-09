using System.ComponentModel.DataAnnotations;

namespace WardMate.Services.AnalyticsSystem.Application;

public sealed record CreateBackupRequest(
    [param: Required, StringLength(64)] string ServiceName,
    [param: Required, StringLength(500, MinimumLength = 3)] string Reason);
public sealed record BackupSummary(Guid Id, string ServiceName, string Status, long? SizeBytes,
    string? Sha256, Guid? RequestedBy, string Reason, DateTime CreatedAtUtc, DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc, DateTime? VerifiedAtUtc, DateTime? RestoreTestedAtUtc, string? ErrorCode);
public interface ISystemBackups
{
    Task<BackupSummary> Create(CreateBackupRequest request, Guid actor, CancellationToken ct);
    Task<BackupSummary> Get(Guid id, CancellationToken ct);
    Task<AnalyticsPage<BackupSummary>> List(string? service, string? status, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct);
    Task<BackupSummary> Verify(Guid id, Guid actor, CancellationToken ct);
}
