namespace WardMate.Services.AIOCR.Domain;

public static class KnowledgeSourceTypes
{
    public const string Procedure = "PROCEDURE";
    public const string ManualText = "MANUAL_TEXT";
    public const string Faq = "FAQ";
    public const string PdfFile = "PDF_FILE";
}

public static class KnowledgeStatuses
{
    public const string Chunked = "CHUNKED";                  // chunks exist, embeddings pending
    public const string Embedded = "EMBEDDED";                // every chunk has an embedding for EmbeddingModel
    public const string EmbeddingFailed = "EMBEDDING_FAILED"; // last attempt failed; retried with backoff
}

public sealed class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string SourceType { get; set; } = KnowledgeSourceTypes.ManualText;
    public Guid? ProcedureId { get; set; }
    public string? SourceReference { get; set; }
    public string? SourceVersion { get; set; }
    public string Content { get; set; } = "";
    public string ContentSha256 { get; set; } = "";
    public string Status { get; set; } = KnowledgeStatuses.Chunked;
    public bool IsActive { get; set; } = true;
    public int ChunkCount { get; set; }
    public string ChunkingVersion { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ChunkedAt { get; set; }
    public string? EmbeddingModel { get; set; }
    public int EmbeddingAttempts { get; set; }
    public DateTime? NextEmbeddingAt { get; set; }
    public string? EmbeddingError { get; set; }
    public DateTime? EmbeddedAt { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public List<KnowledgeChunk> Chunks { get; set; } = [];
}

public sealed class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string? Heading { get; set; }
    public string Content { get; set; } = "";
    public int CharCount { get; set; }
    public string ContentSha256 { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    // L2-normalised vector; null until the indexing worker embeds the chunk with EmbeddingModel.
    public float[]? Embedding { get; set; }
    public string? EmbeddingModel { get; set; }
    public DateTime? EmbeddedAt { get; set; }
}
