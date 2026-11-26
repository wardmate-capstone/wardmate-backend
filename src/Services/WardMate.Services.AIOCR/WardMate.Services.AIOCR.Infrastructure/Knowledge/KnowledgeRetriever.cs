using Microsoft.EntityFrameworkCore;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

// In-process vector index per replica. Sized for a ward-level knowledge base (Knowledge:MaxIndexedChunks, default 20,000),
// so no pgvector is required; rebuilt whenever the searchable set changes.
public sealed class KnowledgeVectorCache
{
    public sealed record Entry(Guid ChunkId, Guid DocumentId, float[] Vector);
    public sealed record Snapshot(string Key, Entry[] Entries);
    private Snapshot? current;
    public Snapshot? Get(string key) { var s = Volatile.Read(ref current); return s?.Key == key ? s : null; }
    public void Set(Snapshot snapshot) => Volatile.Write(ref current, snapshot);
}

public sealed class KnowledgeRetriever(ChatDbContext db, IEmbeddingProvider embedder, KnowledgeSettings settings, KnowledgeVectorCache cache) : IKnowledgeRetriever
{
    public async Task<KnowledgeHit[]> Search(string query, int topK, CancellationToken ct, bool expand = false)
    {
        if (!embedder.Enabled) throw new AiFailure(503, "knowledge.embedding_disabled", "Chưa bật tìm kiếm Knowledge Base (Knowledge:EmbeddingEnabled).");
        var signature = embedder.Signature;
        var searchable = db.KnowledgeChunks.AsNoTracking()
            .Where(c => c.Embedding != null && c.EmbeddingModel == signature && db.KnowledgeDocuments.Any(d => d.Id == c.DocumentId && d.IsActive));
        var count = await searchable.CountAsync(ct);
        if (count == 0) return [];
        if (count > settings.MaxIndexedChunks)
            throw new AiFailure(503, "knowledge.index_too_large", "Knowledge Base vượt giới hạn tìm kiếm trong bộ nhớ; cần chuyển sang pgvector hoặc tăng Knowledge:MaxIndexedChunks.");
        var lastEmbedded = await searchable.MaxAsync(c => c.EmbeddedAt, ct);
        var lastDocChange = await db.KnowledgeDocuments.MaxAsync(d => (DateTime?)d.UpdatedAt, ct);
        var key = $"{signature}|{count}|{lastEmbedded:O}|{lastDocChange:O}";
        var snapshot = cache.Get(key);
        if (snapshot is null)
        {
            var rows = await searchable.Select(c => new { c.Id, c.DocumentId, c.Embedding }).ToListAsync(ct);
            snapshot = new(key, rows.Select(r => new KnowledgeVectorCache.Entry(r.Id, r.DocumentId, r.Embedding!)).ToArray());
            cache.Set(snapshot);
        }
        var vector = await embedder.EmbedQuery(query, ct);
        var top = snapshot.Entries
            .Where(e => e.Vector.Length == vector.Length)
            .Select(e => (e.ChunkId, Score: Vectors.Dot(e.Vector, vector)))
            .Where(x => x.Score >= settings.MinScore)
            .OrderByDescending(x => x.Score).ThenBy(x => x.ChunkId).Take(topK).ToArray();
        if (top.Length == 0) return [];
        var hits = await Load(top, ct);
        if (!expand || hits.Length == 0) return hits;
        // CHAT-CONTEXT-01: đủ mục giấy tờ/phí/thời hạn của thủ tục khớp nhất cho câu hỏi gộp nhiều ý.
        var main = hits[0].DocumentId;
        var mainChunks = await searchable.Where(c => c.DocumentId == main)
            .Select(c => new SectionChunk(c.Id, c.ChunkIndex, c.Heading, c.CharCount)).ToListAsync(ct);
        var extraIds = KnowledgeContext.Expand(query, hits, mainChunks);
        if (extraIds.Length == 0) return hits;
        var vectors = snapshot.Entries.Where(e => extraIds.Contains(e.ChunkId)).ToDictionary(e => e.ChunkId, e => e.Vector);
        var extra = extraIds.Where(vectors.ContainsKey)
            .Select(id => (ChunkId: id, Score: vectors[id].Length == vector.Length ? Vectors.Dot(vectors[id], vector) : 0d)).ToArray();
        return [.. hits, .. await Load(extra, ct)];
    }

    private async Task<KnowledgeHit[]> Load((Guid ChunkId, double Score)[] top, CancellationToken ct)
    {
        if (top.Length == 0) return [];
        var ids = top.Select(x => x.ChunkId).ToArray();
        var details = await db.KnowledgeChunks.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Join(db.KnowledgeDocuments.AsNoTracking().Where(d => d.IsActive), c => c.DocumentId, d => d.Id,
                (c, d) => new { c.Id, c.DocumentId, d.Title, d.SourceType, d.ProcedureId, c.Heading, c.Content })
            .ToDictionaryAsync(x => x.Id, ct);
        // A document deactivated between scoring and loading simply drops out of the result.
        return top.Where(x => details.ContainsKey(x.ChunkId)).Select(x =>
        {
            var d = details[x.ChunkId];
            return new KnowledgeHit(d.Id, d.DocumentId, d.Title, d.SourceType, d.ProcedureId, d.Heading, d.Content, Math.Round(x.Score, 4));
        }).ToArray();
    }
}
