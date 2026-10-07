using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using WardMate.Services.ProcedureCatalog.Application.Drafts;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Drafts;

public sealed class DraftFileStorage(Lazy<IBlobStorageClient> blobs, IConfiguration configuration) : IDraftFileStorage
{
    public const string Container = "procedure-sources";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["AzureBlob:ConnectionString"]);
    public Task<string> Upload(Stream pdf, string blobName, CancellationToken ct) =>
        blobs.Value.UploadAsync(pdf, blobName, "application/pdf", Container, ct);
    public Task<string> ReadUrl(string blobName, CancellationToken ct) =>
        blobs.Value.GenerateSasUrlAsync(blobName, TimeSpan.FromMinutes(10), Container, ct);
}

public sealed class DraftPersistence(ProcedureDbContext db) : IDraftPersistence
{
    public Task<ProcedureDraft?> Find(Guid id, CancellationToken ct) => db.ProcedureDrafts.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<DraftSummaryDto>> List(int page, int pageSize, CancellationToken ct) =>
        await db.ProcedureDrafts.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new DraftSummaryDto(d.Id, d.Status, d.PdfFileName, d.FailureCode, d.Revision,
                d.PublishedProcedureId, d.CreatedAt, d.UpdatedAt)).ToListAsync(ct);
    public async Task Insert(ProcedureDraft draft, CancellationToken ct)
    {
        db.ProcedureDrafts.Add(draft); await db.SaveChangesAsync(ct);
    }
    public async Task<bool> Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
    }
    public Task<Guid?> FindPublishedDraftId(Guid procedureId, CancellationToken ct) =>
        (from d in db.ProcedureDrafts
         join p in db.Procedures on d.PublishedProcedureId equals p.Id
         where p.Id == procedureId && p.IsActive && d.Status == "Published" && p.OriginalPdfUrl == d.OriginalPdfUrl
         select (Guid?)d.Id).FirstOrDefaultAsync(ct);

    public async Task<ProcedureResult<ProcedureDetailDto>> PublishLocked(Guid id,
        Func<ProcedureDraft?, Task<ProcedureResult<ProcedureDetailDto>>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var d = (await db.ProcedureDrafts.FromSqlInterpolated($"SELECT * FROM procedure_drafts WHERE id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            var result = await action(d);
            if (!result.IsSuccess) { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); return result; }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await tx.RollbackAsync(ct); db.ChangeTracker.Clear();
            return ProcedureResult<ProcedureDetailDto>.Fail("procedure.code_exists", "Dữ liệu đã thay đổi đồng thời. Vui lòng tải lại và thử lại.", 409);
        }
    }
}
