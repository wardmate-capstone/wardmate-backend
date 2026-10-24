using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Domain;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

public sealed class KnowledgeBaseService(ChatDbContext db, IProcedureCatalogSource catalog, IKnowledgePdfReader pdf, TimeProvider clock,
    IKnowledgeRetriever retriever, KnowledgeSettings settings) : IKnowledgeBase
{
    private const int MaxSyncProcedures = 1000;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Page<KnowledgeDocumentSummaryDto>> List(KnowledgeListQuery q, CancellationToken ct)
    {
        Paging(q.PageNumber, q.PageSize);
        if (q.SourceType is not null && !IsSourceType(q.SourceType)) Fail(400, "knowledge.validation", "sourceType phải là PROCEDURE, MANUAL_TEXT, FAQ hoặc PDF_FILE.");
        if (q.Search?.Length > 200) Fail(400, "knowledge.validation", "Từ khóa tối đa 200 ký tự.");
        var docs = db.KnowledgeDocuments.AsNoTracking();
        if (q.SourceType is not null) docs = docs.Where(x => x.SourceType == q.SourceType);
        if (q.IsActive is not null) docs = docs.Where(x => x.IsActive == q.IsActive);
        if (q.ProcedureId is not null) docs = docs.Where(x => x.ProcedureId == q.ProcedureId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var pattern = "%" + q.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            docs = docs.Where(x => EF.Functions.ILike(x.Title, pattern, "\\") || x.SourceReference != null && EF.Functions.ILike(x.SourceReference, pattern, "\\"));
        }
        var total = await docs.CountAsync(ct);
        var items = await docs.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new KnowledgeDocumentSummaryDto(x.Id, x.Title, x.SourceType, x.ProcedureId, x.SourceReference, x.SourceVersion,
                x.Status, x.IsActive, x.ChunkCount, x.Content.Length, x.Revision, x.CreatedAt, x.UpdatedAt, x.ChunkedAt)).ToArrayAsync(ct);
        return new(items, q.PageNumber, q.PageSize, total);
    }

    public async Task<KnowledgeDocumentDto> Get(Guid id, CancellationToken ct) =>
        Dto(await db.KnowledgeDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound());

    public async Task<Page<KnowledgeChunkDto>> Chunks(Guid id, int pageNumber, int pageSize, CancellationToken ct)
    {
        Paging(pageNumber, pageSize);
        if (!await db.KnowledgeDocuments.AnyAsync(x => x.Id == id, ct)) throw NotFound();
        var chunks = db.KnowledgeChunks.AsNoTracking().Where(x => x.DocumentId == id);
        var total = await chunks.CountAsync(ct);
        var items = await chunks.OrderBy(x => x.ChunkIndex).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new KnowledgeChunkDto(x.Id, x.ChunkIndex, x.Heading, x.Content, x.CharCount, x.ContentSha256)).ToArrayAsync(ct);
        return new(items, pageNumber, pageSize, total);
    }

    public async Task<KnowledgeDocumentDto> CreateText(Guid actor, CreateKnowledgeTextInput input, CancellationToken ct)
    {
        if (input.SourceType is not (KnowledgeSourceTypes.ManualText or KnowledgeSourceTypes.Faq))
            Fail(400, "knowledge.validation", "sourceType phải là MANUAL_TEXT hoặc FAQ. Thủ tục dùng API đồng bộ, PDF dùng API tải PDF.");
        var title = Title(input.Title);
        var content = Content(input.Content);
        await CheckProcedureReference(input.ProcedureId, ct);
        return await Add(actor, title, input.SourceType, input.ProcedureId, null, null, content, null, ct);
    }

    public async Task<KnowledgeDocumentDto> CreatePdf(Guid actor, KnowledgePdfInput input, Stream file, CancellationToken ct)
    {
        var fileName = Path.GetFileName(input.FileName ?? "").Trim();
        if (fileName.Length is 0 or > 255) Fail(400, "knowledge.validation", "Tên file phải có 1–255 ký tự.");
        var title = Title(string.IsNullOrWhiteSpace(input.Title) ? Path.GetFileNameWithoutExtension(fileName) : input.Title);
        await CheckProcedureReference(input.ProcedureId, ct);
        var text = await pdf.Read(file, ct);
        var content = Content(text.Text);
        var warnings = text.PagesWithoutText.Length == 0 ? null
            : new[] { $"Các trang không đọc được chữ (có thể là ảnh): {string.Join(", ", text.PagesWithoutText)}. Nội dung các trang này không có trong Knowledge Base." };
        return await Add(actor, title, KnowledgeSourceTypes.PdfFile, input.ProcedureId, fileName, $"{text.PageCount} trang", content, warnings, ct);
    }

    public Task<KnowledgeDocumentDto> Update(Guid actor, Guid id, UpdateKnowledgeInput input, CancellationToken ct)
    {
        var title = Title(input.Title);
        return Mutate(id, input.ExpectedRevision, doc =>
        {
            if (doc.SourceType == KnowledgeSourceTypes.Procedure)
                Fail(409, "knowledge.source_not_editable", "Tài liệu thủ tục lấy từ Procedure Catalog; hãy sửa thủ tục rồi đồng bộ lại.");
            if (input.Content is not null && doc.SourceType == KnowledgeSourceTypes.PdfFile)
                Fail(409, "knowledge.source_not_editable", "Nội dung PDF không sửa trực tiếp; hãy tải PDF mới. Chỉ được đổi tiêu đề.");
            doc.Title = title;
            if (input.Content is not null) Replace(doc, Content(input.Content));
            Touch(doc, actor);
        }, ct);
    }

    public Task<KnowledgeDocumentDto> SetActive(Guid actor, Guid id, KnowledgeActivationInput input, CancellationToken ct) =>
        Mutate(id, input.ExpectedRevision, doc =>
        {
            if (doc.IsActive == input.IsActive) Fail(409, "knowledge.no_change", input.IsActive ? "Tài liệu đang được sử dụng." : "Tài liệu đã ngừng sử dụng.");
            doc.IsActive = input.IsActive; Touch(doc, actor);
        }, ct);

    public Task<KnowledgeDocumentDto> Rechunk(Guid actor, Guid id, KnowledgeRevisionInput input, CancellationToken ct) =>
        Mutate(id, input.ExpectedRevision, doc => { Replace(doc, doc.Content); Touch(doc, actor); }, ct);

    public async Task<ProcedureSyncResult> SyncProcedure(Guid actor, Guid procedureId, CancellationToken ct)
    {
        if (procedureId == Guid.Empty) Fail(400, "knowledge.validation", "procedureId không hợp lệ.");
        var procedure = await catalog.Get(procedureId, ct); // HTTP outside the transaction.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialise syncs of the same procedure across replicas before checking for an existing document.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"knowledge-procedure:" + procedureId}, 0))", ct);
        var doc = await db.KnowledgeDocuments.SingleOrDefaultAsync(x => x.SourceType == KnowledgeSourceTypes.Procedure && x.ProcedureId == procedureId, ct);
        string outcome;
        if (procedure is null)
        {
            if (doc is null) Fail(404, "knowledge.procedure_not_found", "Thủ tục không tồn tại hoặc đang ngừng áp dụng trong Procedure Catalog.");
            if (!doc!.IsActive) { await tx.CommitAsync(ct); return new("UNCHANGED", Summary(doc)); }
            doc.IsActive = false; Touch(doc, actor); outcome = "DEACTIVATED";
        }
        else
        {
            var title = Title(procedure.Title);
            var content = Content(ProcedureKnowledgeText.Build(procedure));
            var version = procedure.UpdatedAt.ToUniversalTime().ToString("O");
            if (doc is null)
            {
                doc = NewDocument(actor, title, KnowledgeSourceTypes.Procedure, procedureId, procedure.ProcedureCode, version, content);
                db.KnowledgeDocuments.Add(doc); outcome = "CREATED";
            }
            else if (doc.ContentSha256 == Sha(content) && doc.Title == title && doc.ChunkingVersion == KnowledgeChunker.Version)
            {
                if (doc.SourceVersion != version) { doc.SourceVersion = version; Touch(doc, actor); }
                outcome = "UNCHANGED";
            }
            else
            {
                // Content follows the catalog, but IsActive stays a manager decision: sync never re-enables a document.
                doc.Title = title; doc.SourceReference = procedure.ProcedureCode; doc.SourceVersion = version;
                await db.KnowledgeChunks.Where(x => x.DocumentId == doc.Id).ExecuteDeleteAsync(ct);
                Replace(doc, content); Touch(doc, actor); outcome = "UPDATED";
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(outcome, Summary(doc));
    }

    public async Task<ProcedureSyncAllResult> SyncAllProcedures(Guid actor, CancellationToken ct)
    {
        var listing = await catalog.ListActive(MaxSyncProcedures, ct);
        int created = 0, updated = 0, unchanged = 0, deactivated = 0;
        var failed = new List<Guid>();
        foreach (var id in listing.Ids)
        {
            try
            {
                var result = await SyncProcedure(actor, id, ct);
                switch (result.Outcome) { case "CREATED": created++; break; case "UPDATED": updated++; break; case "DEACTIVATED": deactivated++; break; default: unchanged++; break; }
            }
            catch (AiFailure) { failed.Add(id); db.ChangeTracker.Clear(); }
        }
        // Only a complete listing proves which procedures disappeared; a partial one must not deactivate anything.
        if (listing.Complete)
        {
            var active = listing.Ids.ToHashSet();
            var stale = await db.KnowledgeDocuments.Where(x => x.SourceType == KnowledgeSourceTypes.Procedure && x.IsActive)
                .Select(x => x.ProcedureId!.Value).ToArrayAsync(ct);
            foreach (var id in stale.Where(x => !active.Contains(x)))
            {
                try { if ((await SyncProcedure(actor, id, ct)).Outcome == "DEACTIVATED") deactivated++; }
                catch (AiFailure) { failed.Add(id); db.ChangeTracker.Clear(); }
            }
        }
        return new(listing.Ids.Length, created, updated, unchanged, deactivated, failed.Count, listing.Complete, failed.ToArray());
    }

    private async Task<KnowledgeDocumentDto> Add(Guid actor, string title, string sourceType, Guid? procedureId, string? reference,
        string? version, string content, string[]? warnings, CancellationToken ct)
    {
        var doc = NewDocument(actor, title, sourceType, procedureId, reference, version, content);
        db.KnowledgeDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return Dto(doc) with { Warnings = warnings };
    }

    private KnowledgeDocument NewDocument(Guid actor, string title, string sourceType, Guid? procedureId, string? reference, string? version, string content)
    {
        var doc = new KnowledgeDocument { Title = title, SourceType = sourceType, ProcedureId = procedureId, SourceReference = reference,
            SourceVersion = version, CreatedBy = actor, UpdatedBy = actor, CreatedAt = Now, UpdatedAt = Now };
        Replace(doc, content);
        return doc;
    }

    public Task<KnowledgeDocumentDto> Reindex(Guid actor, Guid id, KnowledgeRevisionInput input, CancellationToken ct) =>
        Mutate(id, input.ExpectedRevision, doc => { ResetEmbedding(doc); Touch(doc, actor); }, ct, clearEmbeddings: true);

    public async Task<KnowledgeIndexStatus> IndexStatus(CancellationToken ct)
    {
        var docs = db.KnowledgeDocuments.AsNoTracking();
        var signature = settings.Signature;
        var searchable = await db.KnowledgeChunks.CountAsync(c => c.Embedding != null && c.EmbeddingModel == signature
            && db.KnowledgeDocuments.Any(d => d.Id == c.DocumentId && d.IsActive), ct);
        return new(settings.EmbeddingEnabled, settings.RagEnabled, signature,
            await docs.CountAsync(x => x.IsActive, ct),
            await docs.CountAsync(x => x.IsActive && x.Status == KnowledgeStatuses.Chunked, ct),
            await docs.CountAsync(x => x.IsActive && x.Status == KnowledgeStatuses.Embedded && x.EmbeddingModel == signature, ct),
            await docs.CountAsync(x => x.IsActive && x.Status == KnowledgeStatuses.EmbeddingFailed, ct),
            await docs.CountAsync(x => !x.IsActive, ct),
            searchable, settings.MaxIndexedChunks, settings.MinScore, settings.TopK);
    }

    public async Task<KnowledgeSearchHitDto[]> Search(KnowledgeSearchInput input, CancellationToken ct)
    {
        var query = (input.Query ?? "").Trim();
        if (query.Length is < 2 or > 1000 || input.TopK is < 1 or > 10) Fail(400, "knowledge.validation", "query phải có 2–1000 ký tự, topK từ 1 đến 10.");
        var hits = await retriever.Search(query, input.TopK, ct);
        return hits.Select((h, i) => new KnowledgeSearchHitDto(i + 1, h.DocumentId, h.DocumentTitle, h.SourceType, h.ProcedureId, h.ChunkId, h.Heading, h.Content, h.Score)).ToArray();
    }

    private async Task<KnowledgeDocumentDto> Mutate(Guid id, Guid expectedRevision, Action<KnowledgeDocument> change, CancellationToken ct, bool clearEmbeddings = false)
    {
        if (expectedRevision == Guid.Empty) Fail(400, "knowledge.validation", "Cần expectedRevision lấy từ lần đọc gần nhất.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var doc = await db.KnowledgeDocuments.FromSqlInterpolated($"SELECT * FROM knowledge_documents WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw NotFound();
        if (doc.Revision != expectedRevision) Fail(409, "knowledge.revision_conflict", "Tài liệu đã thay đổi. Hãy tải lại trước khi thao tác.");
        change(doc);
        // Chunks are not loaded here, so any chunk present now was produced by Replace: drop the stored set first.
        if (doc.Chunks.Count > 0) await db.KnowledgeChunks.Where(x => x.DocumentId == doc.Id).ExecuteDeleteAsync(ct);
        else if (clearEmbeddings) await db.KnowledgeChunks.Where(x => x.DocumentId == doc.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Embedding, (float[]?)null).SetProperty(x => x.EmbeddingModel, (string?)null).SetProperty(x => x.EmbeddedAt, (DateTime?)null), ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Dto(doc);
    }

    // Rebuilds chunks from content; callers persist inside their transaction. Chunks are replaced, never merged.
    private void Replace(KnowledgeDocument doc, string content)
    {
        var drafts = KnowledgeChunker.Split(content);
        if (drafts.Count == 0) Fail(422, "knowledge.empty_content", "Nội dung không tạo được đoạn nào.");
        doc.Content = content; doc.ContentSha256 = Sha(content);
        doc.Chunks = drafts.Select(x => new KnowledgeChunk { DocumentId = doc.Id, ChunkIndex = x.Index, Heading = x.Heading,
            Content = x.Content, CharCount = x.Content.Length, ContentSha256 = Sha(x.Content), CreatedAt = Now }).ToList();
        doc.ChunkCount = drafts.Count; doc.ChunkingVersion = KnowledgeChunker.Version;
        doc.ChunkedAt = Now; ResetEmbedding(doc);
    }

    // New or rebuilt chunks have no vectors; the indexing worker picks the document up again.
    private static void ResetEmbedding(KnowledgeDocument doc)
    {
        doc.Status = KnowledgeStatuses.Chunked; doc.EmbeddingModel = null; doc.EmbeddedAt = null;
        doc.EmbeddingAttempts = 0; doc.NextEmbeddingAt = null; doc.EmbeddingError = null;
    }

    private void Touch(KnowledgeDocument doc, Guid actor) { doc.UpdatedBy = actor; doc.UpdatedAt = Now; doc.Revision = Guid.NewGuid(); }

    private async Task CheckProcedureReference(Guid? procedureId, CancellationToken ct)
    {
        if (procedureId is null) return;
        if (procedureId == Guid.Empty || await catalog.Get(procedureId.Value, ct) is null)
            Fail(422, "knowledge.procedure_not_found", "procedureId không tồn tại hoặc đang ngừng áp dụng trong Procedure Catalog.");
    }

    private static string Title(string? value)
    {
        var title = (value ?? "").Trim();
        if (title.Length is < 3 or > 500 || title.Any(char.IsControl)) Fail(400, "knowledge.validation", "Tiêu đề phải có 3–500 ký tự, không chứa ký tự điều khiển.");
        return title;
    }

    private static string Content(string? value)
    {
        var content = KnowledgeChunker.Normalize(value ?? "");
        if (content.Length < 20) Fail(400, "knowledge.validation", "Nội dung phải có ít nhất 20 ký tự.");
        if (content.Length > KnowledgeChunker.MaxContentChars) Fail(422, "knowledge.content_too_long", "Nội dung vượt quá 200.000 ký tự; hãy tách thành nhiều tài liệu.");
        return content;
    }

    private static void Paging(int page, int size)
    {
        if (page is < 1 or > 100000 || size is < 1 or > 100) Fail(400, "knowledge.validation", "pageNumber phải từ 1 đến 100000, pageSize từ 1 đến 100.");
    }

    private static bool IsSourceType(string value) => value is KnowledgeSourceTypes.Procedure or KnowledgeSourceTypes.ManualText or KnowledgeSourceTypes.Faq or KnowledgeSourceTypes.PdfFile;
    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static AiFailure NotFound() => new(404, "knowledge.not_found", "Không tìm thấy tài liệu tri thức.");
    private static void Fail(int status, string code, string message) => throw new AiFailure(status, code, message);

    private static KnowledgeDocumentSummaryDto Summary(KnowledgeDocument x) => new(x.Id, x.Title, x.SourceType, x.ProcedureId, x.SourceReference,
        x.SourceVersion, x.Status, x.IsActive, x.ChunkCount, x.Content.Length, x.Revision, x.CreatedAt, x.UpdatedAt, x.ChunkedAt);
    private static KnowledgeDocumentDto Dto(KnowledgeDocument x) => new(x.Id, x.Title, x.SourceType, x.ProcedureId, x.SourceReference,
        x.SourceVersion, x.Status, x.IsActive, x.ChunkCount, x.ChunkingVersion, x.Content, x.ContentSha256, x.CreatedBy, x.UpdatedBy,
        x.Revision, x.CreatedAt, x.UpdatedAt, x.ChunkedAt, null, x.EmbeddingModel, x.EmbeddingAttempts, x.NextEmbeddingAt, x.EmbeddingError, x.EmbeddedAt);
}
