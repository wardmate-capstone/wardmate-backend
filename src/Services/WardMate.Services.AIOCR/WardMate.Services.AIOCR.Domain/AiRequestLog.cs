namespace WardMate.Services.AIOCR.Domain;

// One row per Gemini call (or per call rejected by the daily limit). Never stores prompts, answers, files or CCCD data.
public sealed class AiRequestLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Operation { get; set; } = "";
    public string Model { get; set; } = "";
    public string Outcome { get; set; } = AiOutcomes.Success;
    public string? ErrorCode { get; set; }
    public int? PromptTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public int DurationMs { get; set; }
    public int ItemCount { get; set; } = 1;
}

public static class AiOperations
{
    public const string Chat = "CHAT";
    public const string ChatRag = "CHAT_RAG";
    public const string CccdExtract = "CCCD_EXTRACT";
    public const string DocumentVerify = "DOCUMENT_VERIFY";
    public const string EmbedQuery = "EMBED_QUERY";
    public const string EmbedDocuments = "EMBED_DOCUMENTS";
    // Calls a user starts directly; these count toward the daily limits. Embeddings are logged but not counted.
    public static readonly string[] Limited = [Chat, ChatRag, CccdExtract, DocumentVerify];
}

public static class AiOutcomes
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Rejected = "REJECTED";
}
