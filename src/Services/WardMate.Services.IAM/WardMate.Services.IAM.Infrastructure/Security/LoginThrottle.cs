using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WardMate.Services.IAM.Application.Interfaces;

namespace WardMate.Services.IAM.Infrastructure.Security;

/// <summary>
/// IAM-01a: chặn đăng nhập khi một tài khoản sai mật khẩu quá <c>Login:MaxFailures</c> lần (mặc định 10)
/// trong <c>Login:WindowMinutes</c> phút (mặc định 15). Bị chặn đến hết cửa sổ tính từ lần sai đầu tiên.
/// Bộ đếm nằm trong bộ nhớ từng replica: khởi động lại hoặc chạy nhiều replica sẽ đếm riêng.
/// </summary>
public sealed class LoginThrottle : ILoginThrottle
{
    private const int PruneThreshold = 10_000;
    private const int MaxTrackedKeys = 100_000;
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly TimeProvider clock;
    private readonly ILogger<LoginThrottle> logger;
    private readonly int maxFailures;
    private readonly TimeSpan window;
    private int pruning;

    public LoginThrottle(IConfiguration config, TimeProvider clock, ILogger<LoginThrottle> logger)
    {
        this.clock = clock;
        this.logger = logger;
        maxFailures = Math.Clamp(config.GetValue("Login:MaxFailures", 10), 3, 100);
        window = TimeSpan.FromMinutes(Math.Clamp(config.GetValue("Login:WindowMinutes", 15), 1, 1440));
    }

    public TimeSpan? Blocked(string key)
    {
        if (!entries.TryGetValue(key, out var entry)) return null;
        var now = clock.GetUtcNow();
        var ends = entry.WindowStart + window;
        if (ends <= now) { entries.TryRemove(new KeyValuePair<string, Entry>(key, entry)); return null; }
        return entry.Failures >= maxFailures ? ends - now : null;
    }

    public void Failed(string key)
    {
        var now = clock.GetUtcNow();
        if (entries.Count >= PruneThreshold) Prune(now);
        if (entries.Count >= MaxTrackedKeys && !entries.ContainsKey(key))
        {
            // Không để chuỗi đăng nhập ngẫu nhiên làm cạn bộ nhớ; giới hạn IP của endpoint vẫn áp dụng.
            logger.LogWarning("Login throttle is tracking {Count} keys; new key not tracked.", entries.Count);
            return;
        }
        entries.AddOrUpdate(key, _ => new Entry(1, now),
            (_, old) => old.WindowStart + window <= now ? new Entry(1, now) : old with { Failures = old.Failures + 1 });
    }

    public void Reset(string key) => entries.TryRemove(key, out _);

    private void Prune(DateTimeOffset now)
    {
        if (Interlocked.Exchange(ref pruning, 1) == 1) return;
        try
        {
            foreach (var (key, entry) in entries)
                if (entry.WindowStart + window <= now) entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        }
        finally { Volatile.Write(ref pruning, 0); }
    }

    private sealed record Entry(int Failures, DateTimeOffset WindowStart);
}
