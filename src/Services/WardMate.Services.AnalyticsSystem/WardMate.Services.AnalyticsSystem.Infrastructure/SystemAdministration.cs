using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class SystemAdministration(AnalyticsDbContext db, IConfiguration config, TimeProvider clock) : ISystemAdministration
{
    public const string LookbackKey = "analytics.lookbackDays";
    private void EnsureEnabled()
    {
        if (!config.GetValue("SystemAdministration:Enabled", false))
            throw new AnalyticsException(503, "system.administration_disabled", "Quản trị hệ thống chưa bật. Áp dụng migration trước khi bật SystemAdministration:Enabled.");
    }
    private void ValidateKey(string key)
    {
        EnsureEnabled();
        if (key != LookbackKey) throw new AnalyticsException(404, "system.setting_not_found", "Khóa cấu hình không được hỗ trợ.");
    }
    public async Task<SystemSettingDto[]> Settings(CancellationToken ct) => [await Setting(LookbackKey, ct)];
    public async Task<SystemSettingDto> Setting(string key, CancellationToken ct)
    {
        ValidateKey(key);
        var s = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        return s is null ? new(key, config.GetValue("Analytics:LookbackDays", 7), 0, null, null) : Map(s);
    }
    public async Task<SystemSettingDto> Update(string key, UpdateSystemSetting request, AuditActor actor, CancellationToken ct)
    {
        ValidateKey(key);
        if (request.Value is < 1 or > 366 || request.ExpectedVersion < 0 || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 3 or > 500 || actor.UserId == Guid.Empty)
            throw new AnalyticsException(400, "system.invalid_setting", "Giá trị phải từ 1–366, phiên bản không âm và lý do từ 3–500 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializes the first insert too; optimistic version still rejects stale client writes.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(343434, 1)", ct);
        var s = await db.SystemSettings.SingleOrDefaultAsync(x => x.Key == key, ct);
        if ((s?.Version ?? 0) != request.ExpectedVersion)
            throw new AnalyticsException(409, "system.setting_conflict", "Cấu hình đã thay đổi. Đọc lại phiên bản trước khi lưu.");
        var previous = s?.Value ?? config.GetValue("Analytics:LookbackDays", 7);
        if (s is null) { s = new() { Key = key }; db.SystemSettings.Add(s); }
        s.Value = request.Value;
        s.Version = checked(s.Version + 1);
        s.UpdatedBy = actor.UserId;
        s.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.SystemAudits.Add(new SystemAuditLog { Id = Guid.NewGuid(), ActorId = actor.UserId,
            Action = "system.setting.updated", EntityKey = key, PreviousValue = previous, NewValue = s.Value,
            Version = s.Version, Reason = request.Reason.Trim(), ClientIp = actor.ClientIp,
            TraceId = actor.TraceId, OccurredAtUtc = s.UpdatedAtUtc });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(s);
    }
    public async Task<AnalyticsPage<SystemAuditDto>> Audits(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct)
    {
        EnsureEnabled();
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100 || from > to ||
            from is { Kind: not DateTimeKind.Utc } || to is { Kind: not DateTimeKind.Utc })
            throw new AnalyticsException(400, "system.invalid_query", "Dùng ngày UTC có Z, from ≤ to, page 1–100000 và pageSize 1–100.");
        var q = db.SystemAudits.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(x => x.OccurredAtUtc >= from);
        if (to.HasValue) q = q.Where(x => x.OccurredAtUtc <= to);
        var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(Map).ToArray(), page, pageSize, count);
    }
    public async Task<SystemAuditDto> Audit(Guid id, CancellationToken ct)
    {
        EnsureEnabled();
        return Map(await db.SystemAudits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AnalyticsException(404, "system.audit_not_found", "Không tìm thấy nhật ký."));
    }
    private static SystemSettingDto Map(SystemSetting s) => new(s.Key, s.Value, s.Version, s.UpdatedBy, s.UpdatedAtUtc);
    private static SystemAuditDto Map(SystemAuditLog a) => new(a.Id, a.ActorId, a.Action, a.EntityKey, a.PreviousValue,
        a.NewValue, a.Version, a.Reason, a.ClientIp, a.TraceId, a.OccurredAtUtc);
}
