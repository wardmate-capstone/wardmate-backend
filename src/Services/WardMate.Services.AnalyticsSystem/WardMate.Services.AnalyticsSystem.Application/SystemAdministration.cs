using System.ComponentModel.DataAnnotations;

namespace WardMate.Services.AnalyticsSystem.Application;

public sealed record UpdateSystemSetting([property: Range(1, 366)] int Value,
    [property: Range(0, long.MaxValue)] long ExpectedVersion,
    [property: Required, StringLength(500, MinimumLength = 3)] string Reason);
public sealed record SystemSettingDto(string Key, int Value, long Version, Guid? UpdatedBy, DateTime? UpdatedAtUtc);
public sealed record SystemAuditDto(Guid Id, Guid ActorId, string Action, string EntityKey, int? PreviousValue,
    int NewValue, long Version, string Reason, string? ClientIp, string TraceId, DateTime OccurredAtUtc);
public sealed record AuditActor(Guid UserId, string? ClientIp, string TraceId);
public interface ISystemAdministration
{
    Task<SystemSettingDto[]> Settings(CancellationToken ct);
    Task<SystemSettingDto> Setting(string key, CancellationToken ct);
    Task<SystemSettingDto> Update(string key, UpdateSystemSetting request, AuditActor actor, CancellationToken ct);
    Task<AnalyticsPage<SystemAuditDto>> Audits(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct);
    Task<SystemAuditDto> Audit(Guid id, CancellationToken ct);
}
