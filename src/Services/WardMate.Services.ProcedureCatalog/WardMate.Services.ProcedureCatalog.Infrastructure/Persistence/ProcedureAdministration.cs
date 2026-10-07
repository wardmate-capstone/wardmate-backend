using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Infrastructure.Drafts;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public sealed class ProcedureAdministration(ProcedureDbContext db, IProcedureManagementStore management,
    Lazy<IBlobStorageClient> blobs, IConfiguration configuration, IValidator<ProcedureInput> validator,
    ILogger<ProcedureAdministration> logger) : IProcedureAdministration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<ProcedureResult<ProcedureDetailDto>> Detail(Guid id, CancellationToken ct)
    {
        var p = await db.Procedures.AsNoTracking().Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? ProcedureResult<ProcedureDetailDto>.NotFound() : ProcedureResult<ProcedureDetailDto>.Ok(ProcedureDetailDto.From(p));
    }
    public async Task<ProcedureResult<ProcedureCategoryDto>> SaveCategory(int? id, CategoryInput input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize category names, including concurrent rename/create, without changing existing category IDs.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7060101)", ct);
        var name = input.CategoryName.Trim();
        ProcedureCategory? category = null;
        if (id.HasValue)
        {
            category = (await db.ProcedureCategories.FromSqlInterpolated($"SELECT * FROM procedure_categories WHERE id = {id.Value} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if (category is null) return ProcedureResult<ProcedureCategoryDto>.Fail("category.not_found", "Không tìm thấy danh mục.", 404);
        }
        if (await db.ProcedureCategories.AnyAsync(x => (!id.HasValue || x.Id != id.Value) && x.CategoryName.ToLower() == name.ToLower(), ct))
            return ProcedureResult<ProcedureCategoryDto>.Fail("category.name_exists", "Tên danh mục đã tồn tại.", 409);
        if (category is null) { category = new ProcedureCategory(); db.ProcedureCategories.Add(category); }
        category.CategoryName = name;
        category.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ProcedureResult<ProcedureCategoryDto>.Ok(new(category.Id, category.CategoryName, category.Description));
    }
    public async Task<ProcedureResult<bool>> DeleteCategory(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var category = (await db.ProcedureCategories.FromSqlInterpolated($"SELECT * FROM procedure_categories WHERE id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (category is null) return ProcedureResult<bool>.Fail("category.not_found", "Không tìm thấy danh mục.", 404);
        if (await db.Procedures.AnyAsync(x => x.CategoryId == id, ct)) return CategoryInUse();
        db.ProcedureCategories.Remove(category);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); return CategoryInUse(); }
        return ProcedureResult<bool>.Ok(true);
    }
    private static ProcedureResult<bool> CategoryInUse() => ProcedureResult<bool>.Fail("category.in_use", "Không thể xóa danh mục đang có thủ tục liên kết.", 409);

    public Task<ProcedureResult<ProcedureDetailDto>> Rollback(RollbackProcedureCommand r, CancellationToken ct) => management.Transaction(async () =>
    {
        var current = await management.LockProcedure(r.Id, ct);
        if (current is null) return ProcedureResult<ProcedureDetailDto>.NotFound();
        var version = await db.ProcedureVersions.AsNoTracking().SingleOrDefaultAsync(x => x.ProcedureId == r.Id && x.VersionNumber == r.VersionNumber, ct);
        if (version is null) return ProcedureResult<ProcedureDetailDto>.Fail("procedure.version_not_found", "Không tìm thấy phiên bản của thủ tục.", 404);
        ProcedureInput? snapshot;
        try
        {
            using var parsed = JsonDocument.Parse(version.SnapshotData);
            if (!parsed.RootElement.TryGetProperty("id", out var sourceId) || sourceId.GetGuid() != current.Id)
                return InvalidSnapshot();
            snapshot = JsonSerializer.Deserialize<ProcedureInput>(version.SnapshotData, Json);
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException) { return InvalidSnapshot(); }
        if (snapshot is null || !(await validator.ValidateAsync(snapshot, ct)).IsValid) return InvalidSnapshot();
        var category = await management.FindCategory(snapshot.CategoryId, ct);
        if (category is null) return ProcedureResult<ProcedureDetailDto>.Fail("procedure.category_not_found", "Danh mục trong phiên bản đã bị xóa. Cần tạo hoặc chọn lại danh mục qua cập nhật thủ công.", 409);
        if (await management.CodeExists(snapshot.ProcedureCode.Trim(), current.Id, ct))
            return ProcedureResult<ProcedureDetailDto>.Fail("procedure.code_exists", "Mã thủ tục trong phiên bản đã được thủ tục khác sử dụng.", 409);
        var now = DateTime.UtcNow;
        var before = ProcedureVersion.Capture(current, await management.NextVersion(current.Id, ct), r.Input.EffectiveDate, r.Input.DecisionNumber.Trim(), now);
        // Keep the existing version convention: snapshot is the state BEFORE the mutation.
        var audit = JsonNode.Parse(before.SnapshotData)!.AsObject();
        audit["rollback"] = JsonSerializer.SerializeToNode(new { restoredFromVersion = r.VersionNumber,
            reason = r.Input.Reason.Trim(), changedBy = r.Actor, decisionNumber = r.Input.DecisionNumber.Trim(), effectiveDate = r.Input.EffectiveDate }, Json);
        before.SnapshotData = audit.ToJsonString();
        management.AddVersion(before);
        snapshot.ApplyTo(current);
        current.ContentPayload.DecisionNumber = r.Input.DecisionNumber.Trim();
        current.Category = category; current.UpdatedAt = now;
        return ProcedureResult<ProcedureDetailDto>.Ok(ProcedureDetailDto.From(current));
    }, ct);
    private static ProcedureResult<ProcedureDetailDto> InvalidSnapshot() => ProcedureResult<ProcedureDetailDto>.Fail("procedure.invalid_snapshot", "Phiên bản không đủ dữ liệu hợp lệ để khôi phục. Vui lòng đối soát và cập nhật thủ công.", 409);

    public async Task<ProcedureResult<bool>> DiscardDraft(Guid id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configuration["AzureBlob:ConnectionString"])) return StorageFailure<bool>();
        string blobName;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var d = (await db.ProcedureDrafts.FromSqlInterpolated($"SELECT * FROM procedure_drafts WHERE id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if (d is null) return ProcedureResult<bool>.Fail("draft.not_found", "Không tìm thấy bản nháp.", 404);
            if (d.Status is not ("NeedsReview" or "Failed" or "Queued" or "Deleting") || d.PublishedProcedureId.HasValue ||
                await db.Procedures.AnyAsync(p => p.OriginalPdfUrl == d.OriginalPdfUrl, ct) ||
                await db.ProcedureVersions.AnyAsync(v => v.OriginalPdfUrl == d.OriginalPdfUrl, ct))
                return ProcedureResult<bool>.Fail("draft.delete_conflict", "Không thể xóa bản nháp đang xử lý, đã xuất bản hoặc PDF đang được thủ tục/lịch sử sử dụng.", 409);
            blobName = d.BlobName;
            // Durable deletion intent: worker/publish/save cannot reuse this draft if Blob or DB fails.
            d.Status = "Deleting"; d.Revision = Guid.NewGuid(); d.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        try { await blobs.Value.DeleteAsync(blobName, DraftFileStorage.Container, ct); }
        catch (Exception e) when (IsStorageError(e, ct))
        { logger.LogWarning("Xóa PDF bản nháp {DraftId} thất bại: {ErrorType}", id, e.GetType().Name); return StorageFailure<bool>(); }
        // Repeated DELETE is safe after a Blob success / database failure.
        await db.ProcedureDrafts.Where(x => x.Id == id && x.Status == "Deleting").ExecuteDeleteAsync(ct);
        return ProcedureResult<bool>.Ok(true);
    }
    public async Task<ProcedureResult<SourceLinkDto>> VersionSource(Guid id, Guid versionId, CancellationToken ct)
    {
        var version = await db.ProcedureVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.ProcedureId == id, ct);
        if (version is null) return ProcedureResult<SourceLinkDto>.Fail("procedure.version_not_found", "Không tìm thấy phiên bản của thủ tục.", 404);
        if (string.IsNullOrWhiteSpace(version.OriginalPdfUrl)) return SourceMissing();
        // Only sign blobs recorded by our upload flow; never sign arbitrary URLs supplied by clients.
        var draft = await db.ProcedureDrafts.AsNoTracking().FirstOrDefaultAsync(x => x.PublishedProcedureId == id &&
            x.Status == "Published" && x.OriginalPdfUrl == version.OriginalPdfUrl, ct);
        if (draft is null) return SourceMissing();
        if (string.IsNullOrWhiteSpace(configuration["AzureBlob:ConnectionString"])) return StorageFailure<SourceLinkDto>();
        try
        {
            if (!await blobs.Value.ExistsAsync(draft.BlobName, DraftFileStorage.Container, ct)) return SourceMissing();
            return ProcedureResult<SourceLinkDto>.Ok(new(await blobs.Value.GenerateSasUrlAsync(draft.BlobName, TimeSpan.FromMinutes(10), DraftFileStorage.Container, ct)));
        }
        catch (Exception e) when (IsStorageError(e, ct))
        { logger.LogWarning("Đọc nguồn phiên bản {VersionId} thất bại: {ErrorType}", versionId, e.GetType().Name); return StorageFailure<SourceLinkDto>(); }
    }
    private static bool IsStorageError(Exception e, CancellationToken ct) => e is RequestFailedException or FormatException or InvalidOperationException or HttpRequestException || e is OperationCanceledException && !ct.IsCancellationRequested;
    private static ProcedureResult<T> StorageFailure<T>() => ProcedureResult<T>.Fail("draft.storage_unavailable", "Lưu trữ PDF chưa sẵn sàng. Kiểm tra cấu hình Blob và thử lại.", 503);
    private static ProcedureResult<SourceLinkDto> SourceMissing() => ProcedureResult<SourceLinkDto>.Fail("procedure.source_not_found", "Phiên bản không có PDF được quản lý bởi hệ thống hoặc file đã mất.", 404);
}
