using Microsoft.Extensions.Configuration;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

// Read on every use so a configuration reload takes effect without restarting the worker.
public sealed class KnowledgeSettings(IConfiguration config)
{
    public bool EmbeddingEnabled => config.GetValue<bool>("Knowledge:EmbeddingEnabled");
    public bool RagEnabled => config.GetValue<bool>("Knowledge:RagEnabled");
    public string EmbeddingModel => config["Knowledge:EmbeddingModel"] is { Length: > 0 } model ? model : "gemini-embedding-001";
    public int EmbeddingDimensions => config.GetValue("Knowledge:EmbeddingDimensions", 768) is var d && d is 768 or 1536 or 3072 ? d : 768;
    public int TopK => Math.Clamp(config.GetValue("Knowledge:TopK", 5), 1, 10);
    public double MinScore => Math.Clamp(config.GetValue("Knowledge:MinScore", 0.55), 0, 1);
    public int MaxIndexedChunks => Math.Clamp(config.GetValue("Knowledge:MaxIndexedChunks", 20_000), 1, 100_000);
    /// <summary>KB-STALE-01: chu kỳ tắt nguồn của thủ tục đã ngừng công khai (phút, 0 = tắt tác vụ). Mặc định 60.</summary>
    public TimeSpan StaleCheckInterval => TimeSpan.FromMinutes(Math.Clamp(config.GetValue("Knowledge:StaleCheckMinutes", 60), 0, 1440));
    public TimeSpan WorkerInterval =>TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Knowledge:WorkerIntervalSeconds", 10), 2, 3600));
    public int MaxEmbeddingAttempts => Math.Clamp(config.GetValue("Knowledge:MaxEmbeddingAttempts", 5), 1, 20);
    public string Signature => $"{EmbeddingModel}@{EmbeddingDimensions}";
}
