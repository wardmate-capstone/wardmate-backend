namespace WardMate.Services.AIOCR.Application;

public sealed record EmbeddingInput(string? Title, string Text);
public interface IEmbeddingProvider
{
    bool Enabled { get; }
    // Model name plus dimension, e.g. "gemini-embedding-001@768"; vectors from different signatures are never compared.
    string Signature { get; }
    Task<float[][]> EmbedDocuments(IReadOnlyList<EmbeddingInput> inputs, CancellationToken ct);
    Task<float[]> EmbedQuery(string text, CancellationToken ct);
}

public sealed record KnowledgeHit(Guid ChunkId, Guid DocumentId, string DocumentTitle, string SourceType, Guid? ProcedureId,
    string? Heading, string Content, double Score);
public interface IKnowledgeRetriever
{
    Task<KnowledgeHit[]> Search(string query, int topK, CancellationToken ct);
}

public sealed record ChatCitation(int Index, Guid DocumentId, string Title, string SourceType, Guid? ProcedureId, Guid ChunkId, string? Heading, double Score);
public sealed record RelatedProcedure(Guid ProcedureId, string Title);

public sealed record KnowledgeSearchInput(string Query, int TopK = 5);
public sealed record KnowledgeSearchHitDto(int Rank, Guid DocumentId, string Title, string SourceType, Guid? ProcedureId,
    Guid ChunkId, string? Heading, string Content, double Score);
public sealed record KnowledgeIndexStatus(bool EmbeddingEnabled, bool RagEnabled, string EmbeddingSignature,
    int ActiveDocuments, int Chunked, int Embedded, int EmbeddingFailed, int Inactive, int SearchableChunks,
    int MaxIndexedChunks, double MinScore, int TopK);

public static class Vectors
{
    public static float[] Normalize(float[] v)
    {
        double sum = 0;
        foreach (var x in v) sum += (double)x * x;
        if (sum <= 0 || !double.IsFinite(sum)) throw new AiFailure(502, "knowledge.embedding_invalid", "Vector embedding không hợp lệ.");
        var norm = Math.Sqrt(sum);
        return v.Select(x => (float)(x / norm)).ToArray();
    }

    // Both vectors are L2-normalised, so the dot product is the cosine similarity.
    public static double Dot(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++) sum += (double)a[i] * b[i];
        return sum;
    }
}
