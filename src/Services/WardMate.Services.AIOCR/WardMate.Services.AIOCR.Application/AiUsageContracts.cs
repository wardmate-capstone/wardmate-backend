namespace WardMate.Services.AIOCR.Application;

public sealed record AiUsageEntry(string Operation, string Model, string Outcome, string? ErrorCode, int DurationMs,
    int? PromptTokens = null, int? OutputTokens = null, int? TotalTokens = null, int ItemCount = 1);

public interface IAiUsage
{
    // Throws AiFailure 429 when the caller or the whole system has used up today's AI calls (UTC+07 day).
    Task Admit(string operation, CancellationToken ct);
    // Best effort: a logging failure never fails the AI call itself.
    Task Record(AiUsageEntry entry, CancellationToken ct);
    Task<AiUsageReport> Report(DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed record AiUsageDay(DateOnly Date, string Operation, int Success, int Failed, int Rejected,
    long PromptTokens, long OutputTokens, long TotalTokens);
public sealed record AiUsageReport(DateOnly From, DateOnly To, int DailyRequestsPerUser, int DailyRequestsTotal,
    int MaxOutputTokens, int UsedToday, IReadOnlyList<AiUsageDay> Days);
