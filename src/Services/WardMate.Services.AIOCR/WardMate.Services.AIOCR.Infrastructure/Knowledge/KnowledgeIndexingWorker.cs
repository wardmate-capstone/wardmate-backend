using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Domain;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

// Embeds chunks of active documents one document at a time. Indexing never changes a document's Revision:
// it is derived state, so it must not invalidate a manager's pending edit.
public sealed class KnowledgeIndexingWorker(IServiceScopeFactory scopes, KnowledgeSettings settings, IConfiguration config,
    TimeProvider clock, ILogger<KnowledgeIndexingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                if (settings.EmbeddingEnabled && !string.IsNullOrWhiteSpace(config.GetConnectionString("AiOcrDatabase")))
                    worked = await ProcessOne(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("Knowledge indexing pass failed ({Type}); retrying later.", e.GetType().Name); }
            if (!worked) await Task.Delay(settings.WorkerInterval, ct);
        }
    }

    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
        var signature = settings.Signature;
        var maxAttempts = settings.MaxEmbeddingAttempts;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // SKIP LOCKED: replicas share the queue, and a document being edited is simply picked up next pass.
        var doc = await db.KnowledgeDocuments.FromSqlInterpolated($"""
            SELECT * FROM knowledge_documents
            WHERE is_active AND (status = 'CHUNKED'
               OR (status = 'EMBEDDING_FAILED' AND embedding_attempts < {maxAttempts} AND (next_embedding_at IS NULL OR next_embedding_at <= {now}))
               OR (status = 'EMBEDDED' AND (embedding_model IS NULL OR embedding_model <> {signature})))
            ORDER BY updated_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(ct);
        if (doc is null) { await tx.RollbackAsync(ct); return false; }
        var pending = await db.KnowledgeChunks.Where(x => x.DocumentId == doc.Id && (x.Embedding == null || x.EmbeddingModel != signature))
            .OrderBy(x => x.ChunkIndex).ToListAsync(ct);
        try
        {
            foreach (var batch in pending.Chunk(GeminiEmbeddingProvider.MaxBatch))
            {
                var vectors = await embedder.EmbedDocuments(batch.Select(x => new EmbeddingInput(
                    x.Heading is null ? doc.Title : $"{doc.Title} — {x.Heading}", x.Content)).ToArray(), ct);
                for (var i = 0; i < batch.Length; i++)
                { batch[i].Embedding = vectors[i]; batch[i].EmbeddingModel = signature; batch[i].EmbeddedAt = now; }
            }
            doc.Status = KnowledgeStatuses.Embedded; doc.EmbeddingModel = signature; doc.EmbeddedAt = now;
            doc.EmbeddingAttempts = 0; doc.NextEmbeddingAt = null; doc.EmbeddingError = null;
        }
        catch (AiFailure e) when (!ct.IsCancellationRequested)
        {
            // Chunks embedded before the failure keep their vectors; only the remainder is retried.
            doc.Status = KnowledgeStatuses.EmbeddingFailed; doc.EmbeddingError = e.Code;
            doc.EmbeddingAttempts++;
            doc.NextEmbeddingAt = now.AddMinutes(Math.Min(60, Math.Pow(2, doc.EmbeddingAttempts)));
            logger.LogWarning("Knowledge document {DocumentId} embedding failed: {Code}, attempt {Attempt}.", doc.Id, e.Code, doc.EmbeddingAttempts);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return true;
    }
}
