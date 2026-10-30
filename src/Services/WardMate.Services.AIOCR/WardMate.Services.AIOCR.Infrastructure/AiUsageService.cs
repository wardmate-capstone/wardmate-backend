using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Domain;

namespace WardMate.Services.AIOCR.Infrastructure;

// Daily limits keep Gemini inside its free quota. Counting is per UTC+07 day and not locked: concurrent calls
// at the limit may pass by a few requests. Logs use their own DbContext so a rolled-back chat turn keeps its log row.
public sealed class AiUsageService(DbContextOptions<ChatDbContext> options, IConfiguration config, IHttpContextAccessor http,
    TimeProvider clock, ILogger<AiUsageService> logger) : IAiUsage
{
    public const int DefaultPerUser = 30;
    public const int DefaultTotal = 500;
    public const int DefaultMaxOutputTokens = 4096;

    public static int PerUser(IConfiguration c) => Math.Clamp(c.GetValue("Assistant:DailyRequestsPerUser", DefaultPerUser), 0, 10_000);
    public static int Total(IConfiguration c) => Math.Clamp(c.GetValue("Assistant:DailyRequestsTotal", DefaultTotal), 0, 1_000_000);
    public static int MaxOutputTokens(IConfiguration c) => Math.Clamp(c.GetValue("Gemini:MaxOutputTokens", DefaultMaxOutputTokens), 256, 8192);

    private bool Configured => !string.IsNullOrWhiteSpace(config.GetConnectionString("AiOcrDatabase"));
    private Guid? Caller => Guid.TryParse(http.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
    private DateTime TodayStartUtc()
    {
        var local = clock.GetUtcNow().UtcDateTime.AddHours(7);
        return DateTime.SpecifyKind(local.Date.AddHours(-7), DateTimeKind.Utc);
    }
    private ChatDbContext Open() => new(options);

    public async Task Admit(string operation, CancellationToken ct)
    {
        // Without the AIOCR database there is nowhere to count; chat already requires it, CCCD/verify then run unlimited.
        if (!Configured || !AiOperations.Limited.Contains(operation)) return;
        var perUser = PerUser(config); var total = Total(config);
        if (perUser == 0 && total == 0) return;
        var since = TodayStartUtc(); var user = Caller;
        await using var db = Open();
        var today = db.AiRequestLogs.AsNoTracking()
            .Where(x => x.CreatedAt >= since && x.Outcome != AiOutcomes.Rejected && AiOperations.Limited.Contains(x.Operation));
        if (total > 0 && await today.CountAsync(ct) >= total)
            await Reject(operation, "ai.daily_limit_reached", "Hệ thống đã dùng hết lượt AI miễn phí hôm nay. Vui lòng thử lại vào ngày mai.", ct);
        if (perUser > 0 && user is Guid id && await today.CountAsync(x => x.UserId == id, ct) >= perUser)
            await Reject(operation, "ai.quota_exceeded", $"Bạn đã dùng hết {perUser} lượt AI hôm nay. Vui lòng thử lại vào ngày mai.", ct);
    }

    private async Task Reject(string operation, string code, string message, CancellationToken ct)
    {
        await Record(new(operation, config["Gemini:Model"] ?? "", AiOutcomes.Rejected, code, 0), ct);
        throw new AiFailure(429, code, message);
    }

    public async Task Record(AiUsageEntry e, CancellationToken ct)
    {
        if (!Configured) return;
        try
        {
            await using var db = Open();
            db.AiRequestLogs.Add(new()
            {
                CreatedAt = clock.GetUtcNow().UtcDateTime, UserId = Caller, Operation = e.Operation, Model = Clip(e.Model, 120),
                Outcome = e.Outcome, ErrorCode = e.ErrorCode is null ? null : Clip(e.ErrorCode, 100), DurationMs = Math.Max(0, e.DurationMs),
                PromptTokens = e.PromptTokens, OutputTokens = e.OutputTokens, TotalTokens = e.TotalTokens, ItemCount = Math.Max(1, e.ItemCount)
            });
            // Not tied to the request token: the call already happened and should be counted even if the client left.
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception error) when (error is DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException)
        {
            logger.LogWarning("AI usage log not saved: Operation={Operation} Outcome={Outcome} ExceptionType={ExceptionType}",
                e.Operation, e.Outcome, error.GetType().Name);
        }
    }

    public async Task<AiUsageReport> Report(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!Configured) throw new AiFailure(503, "chat.not_configured", "Chưa cấu hình database AIOCR.");
        if (to < from || to.DayNumber - from.DayNumber > 92) throw new AiFailure(400, "ai.invalid_range", "Khoảng ngày phải tăng dần, tối đa 93 ngày.");
        var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        await using var db = Open();
        var rows = await db.AiRequestLogs.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .GroupBy(x => new { Day = x.CreatedAt.AddHours(7).Date, x.Operation })
            .Select(g => new
            {
                g.Key.Day, g.Key.Operation,
                Success = g.Count(x => x.Outcome == AiOutcomes.Success),
                Failed = g.Count(x => x.Outcome == AiOutcomes.Failed),
                Rejected = g.Count(x => x.Outcome == AiOutcomes.Rejected),
                Prompt = g.Sum(x => (long?)x.PromptTokens) ?? 0,
                Output = g.Sum(x => (long?)x.OutputTokens) ?? 0,
                Total = g.Sum(x => (long?)x.TotalTokens) ?? 0
            }).ToListAsync(ct);
        var since = TodayStartUtc();
        var used = await db.AiRequestLogs.AsNoTracking()
            .CountAsync(x => x.CreatedAt >= since && x.Outcome != AiOutcomes.Rejected && AiOperations.Limited.Contains(x.Operation), ct);
        return new(from, to, PerUser(config), Total(config), MaxOutputTokens(config), used, rows
            .Select(r => new AiUsageDay(DateOnly.FromDateTime(r.Day), r.Operation, r.Success, r.Failed, r.Rejected, r.Prompt, r.Output, r.Total))
            .OrderBy(x => x.Date).ThenBy(x => x.Operation).ToList());
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}
