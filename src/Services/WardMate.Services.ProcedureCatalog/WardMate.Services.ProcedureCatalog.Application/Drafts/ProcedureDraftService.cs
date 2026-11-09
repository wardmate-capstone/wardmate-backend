using System.Text.Json;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Application.Drafts;

public sealed class ProcedureDraftService(IDraftPersistence store, IDraftFileStorage blobs, ISender sender,
    WardMate.Services.ProcedureCatalog.Application.Interfaces.IProcedureManagementStore procedures,
    DraftProcessingOptions configuration) : IProcedureDraftService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static DraftDto Dto(ProcedureDraft d) => new(d.Id, d.Status, d.PdfFileName,
        JsonSerializer.Deserialize<JsonElement>(d.PayloadJson), JsonSerializer.Deserialize<JsonElement>(d.WarningsJson),
        d.ExtractedText, d.FailureCode, d.Revision, d.PublishedProcedureId, d.CreatedAt, d.UpdatedAt);

    public async Task<ProcedureResult<DraftDto>> Upload(Stream pdf, string fileName, string actor, CancellationToken ct)
    {
        var (name, invalid) = await CheckPdf(pdf, fileName, ct);
        if (invalid is not null) return ProcedureResult<DraftDto>.Fail(invalid.Code, invalid.Message, invalid.Status);
        var now = DateTime.UtcNow;
        var d = new ProcedureDraft { Id = Guid.NewGuid(), CreatedBy = actor, PdfFileName = name, CreatedAt = now, UpdatedAt = now };
        var enabled = configuration.ExtractionEnabled;
        d.Status = enabled ? "Queued" : "NeedsReview";
        d.WarningsJson = JsonSerializer.Serialize(new[] { enabled
            ? "Dữ liệu AI cần được kiểm tra với PDF gốc trước khi xuất bản."
            : "Chưa bật AI/OCR. Vui lòng nhập thông tin từ PDF và đối soát thủ công." });
        d.PayloadJson = "{\"categoryId\":0,\"procedureCode\":\"\",\"title\":\"\",\"levelOfImplementation\":\"\",\"targetAudience\":\"\",\"feeSummary\":\"\",\"processingTimeSummary\":\"\",\"contentPayload\":{\"cases\":[],\"submissionMethods\":[],\"legalReferences\":[],\"results\":[]},\"checklistSchema\":[],\"formDefinitions\":[]}";
        d.BlobName = $"drafts/{d.Id}/source.pdf";
        d.OriginalPdfUrl = await blobs.Upload(pdf, d.BlobName, ct);
        await store.Insert(d, ct);
        return ProcedureResult<DraftDto>.Ok(Dto(d));
    }

    public async Task<ProcedureResult<DraftDto>> Get(Guid id, CancellationToken ct)
    {
        var d = await store.Find(id, ct);
        return d is null ? Missing<DraftDto>() : ProcedureResult<DraftDto>.Ok(Dto(d));
    }
    public Task<IReadOnlyList<DraftSummaryDto>> List(int page, int pageSize, CancellationToken ct) => store.List(page, pageSize, ct);

    public Task<ProcedureResult<DraftDto>> Save(Guid id, SaveDraftInput input, CancellationToken ct)
    {
        if (input.Payload.ValueKind != JsonValueKind.Object || input.Payload.GetRawText().Length > 500_000)
            return Task.FromResult(ProcedureResult<DraftDto>.Fail("draft.invalid_payload", "Bản nháp phải là JSON object tối đa 500.000 ký tự.", 400));
        return Mutate(id, input.Revision, d =>
        {
            if (d.Status is not ("NeedsReview" or "Failed")) return Conflict<DraftDto>();
            d.PayloadJson = input.Payload.GetRawText(); d.Status = "NeedsReview"; d.FailureCode = null;
            return ProcedureResult<DraftDto>.Ok(Dto(d));
        }, ct);
    }
    public Task<ProcedureResult<DraftDto>> Retry(Guid id, Guid revision, CancellationToken ct)
    {
        if (!configuration.ExtractionEnabled)
            return Task.FromResult(ProcedureResult<DraftDto>.Fail("draft.extraction_disabled", "Chưa cấu hình AI/OCR.", 503));
        return Mutate(id, revision, d =>
        {
            if (d.Status != "Failed") return Conflict<DraftDto>();
            d.Status = "Queued"; d.Attempts = 0; d.FailureCode = null;
            return ProcedureResult<DraftDto>.Ok(Dto(d));
        }, ct);
    }
    private async Task<ProcedureResult<DraftDto>> Mutate(Guid id, Guid revision,
        Func<ProcedureDraft, ProcedureResult<DraftDto>> change, CancellationToken ct)
    {
        var d = await store.Find(id, ct);
        if (d is null) return Missing<DraftDto>();
        if (d.Revision != revision) return Conflict<DraftDto>();
        var result = change(d);
        if (!result.IsSuccess) return result;
        d.Revision = Guid.NewGuid(); d.UpdatedAt = DateTime.UtcNow;
        if (!await store.Save(ct)) return Conflict<DraftDto>();
        return ProcedureResult<DraftDto>.Ok(Dto(d));
    }
    public async Task<ProcedureResult<string>> ReadUrl(Guid id, CancellationToken ct)
    {
        if (!blobs.IsConfigured)
            return ProcedureResult<string>.Fail("draft.storage_not_configured", "Chưa cấu hình lưu trữ PDF Azure Blob.", 503);
        var d = await store.Find(id, ct);
        return d is null ? Missing<string>() : ProcedureResult<string>.Ok(
            await blobs.ReadUrl(d.BlobName, ct));
    }
    public async Task<ProcedureResult<string>> PublishedReadUrl(Guid procedureId, CancellationToken ct)
    {
        var draftId = await store.FindPublishedDraftId(procedureId, ct);
        return draftId.HasValue ? await ReadUrl(draftId.Value, ct)
            : ProcedureResult<string>.Fail("draft.source_not_found", "Thủ tục không có PDF được tải lên qua hệ thống bản nháp.", 404);
    }
    public async Task<ProcedureResult<ProcedureDetailDto>> Publish(Guid id, ConfirmDraftInput input, string actor, CancellationToken ct)
    {
        if (!input.Confirmed) return ProcedureResult<ProcedureDetailDto>.Fail("draft.confirmation_required", "Cần xác nhận đã đối soát PDF và nội dung bản nháp.", 400);
        return await store.PublishLocked(id, async d =>
        {
            if (d is null) return Missing<ProcedureDetailDto>();
            if (d.Revision != input.Revision || d.Status != "NeedsReview") return Conflict<ProcedureDetailDto>();
            var missing = DraftPayloadRequirements.Check(JsonSerializer.Deserialize<JsonElement>(d.PayloadJson));
            if (missing.Count > 0) return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Cần bổ sung dữ liệu bản nháp trước khi xuất bản.", 400, missing);
            ReviewedProcedureInput? payload;
            try { payload = JsonSerializer.Deserialize<ReviewedProcedureInput>(d.PayloadJson, Json); }
            catch (JsonException) { return ProcedureResult<ProcedureDetailDto>.Fail("draft.invalid_payload", "Bản nháp không đúng cấu trúc thủ tục.", 400); }
            if (payload is null) return ProcedureResult<ProcedureDetailDto>.Fail("draft.invalid_payload", "Nội dung bản nháp không được để trống.", 400);
            payload.OriginalPdfUrl = d.OriginalPdfUrl; payload.PdfFileName = d.PdfFileName;
            var result = await sender.Send(new PublishReviewedProcedureCommand(payload), ct);
            if (!result.IsSuccess) return result;
            d.Status = "Published"; d.PublishedProcedureId = result.Value!.Id; d.ReviewedBy = actor;
            d.Revision = Guid.NewGuid(); d.UpdatedAt = DateTime.UtcNow;

            return result;
        }, ct);
    }
    /// <summary>
    /// Gắn PDF gốc cho thủ tục đã có (thường là thủ tục nhập tay). PDF được lưu như một bản nháp
    /// trạng thái Published trỏ tới thủ tục, nên GET /procedures/{id}/source và nguồn phiên bản dùng lại luồng ký SAS sẵn có.
    /// </summary>
    public async Task<ProcedureResult<ProcedureDetailDto>> AttachSource(Guid procedureId, Stream pdf, string fileName, string actor, CancellationToken ct)
    {
        var (name, invalid) = await CheckPdf(pdf, fileName, ct);
        if (invalid is not null) return ProcedureResult<ProcedureDetailDto>.Fail(invalid.Code, invalid.Message, invalid.Status);
        if (!await procedures.Exists(procedureId, ct)) return ProcedureResult<ProcedureDetailDto>.NotFound();
        var now = DateTime.UtcNow;
        var d = new ProcedureDraft
        {
            Id = Guid.NewGuid(), Status = "Published", CreatedBy = actor, ReviewedBy = actor, PdfFileName = name,
            PublishedProcedureId = procedureId, CreatedAt = now, UpdatedAt = now,
            WarningsJson = JsonSerializer.Serialize(new[] { "PDF được gắn trực tiếp cho thủ tục; không qua bước đọc AI/OCR." })
        };
        d.BlobName = $"drafts/{d.Id}/source.pdf";
        // Blob trước, DB sau: nếu DB lỗi chỉ còn blob mồ côi, không có thủ tục trỏ tới file không tồn tại.
        d.OriginalPdfUrl = await blobs.Upload(pdf, d.BlobName, ct);
        return await procedures.Transaction(async () =>
        {
            var procedure = await procedures.LockProcedure(procedureId, ct);
            if (procedure is null) return ProcedureResult<ProcedureDetailDto>.NotFound();
            procedures.AddVersion(ProcedureVersion.Capture(procedure, await procedures.NextVersion(procedureId, ct),
                DateOnly.FromDateTime(now), procedure.ContentPayload.DecisionNumber, now));
            store.Add(d);
            procedure.OriginalPdfUrl = d.OriginalPdfUrl;
            procedure.PdfFileName = name;
            procedure.UpdatedAt = now;
            return ProcedureResult<ProcedureDetailDto>.Ok(ProcedureDetailDto.From(procedure));
        }, ct);
    }

    private sealed record PdfProblem(string Code, string Message, int Status);
    private async Task<(string Name, PdfProblem? Problem)> CheckPdf(Stream pdf, string fileName, CancellationToken ct)
    {
        if (!blobs.IsConfigured)
            return (string.Empty, new("draft.storage_not_configured", "Chưa cấu hình lưu trữ PDF Azure Blob.", 503));
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        if (name.Length is 0 or > 255 || !name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return (name, new("draft.invalid_pdf", "Vui lòng tải file PDF có tên tối đa 255 ký tự.", 400));
        var signature = new byte[5];
        if (!pdf.CanSeek || pdf.Length is < 5 or > 20971520 || await pdf.ReadAsync(signature, ct) != 5 ||
            !signature.AsSpan().SequenceEqual("%PDF-"u8))
            return (name, new("draft.invalid_pdf", "File phải là PDF, dung lượng tối đa 20 MB.", 400));
        pdf.Position = 0;
        return (name, null);
    }

    private static ProcedureResult<T> Missing<T>() => ProcedureResult<T>.Fail("draft.not_found", "Không tìm thấy bản nháp.", 404);
    private static ProcedureResult<T> Conflict<T>() => ProcedureResult<T>.Fail("draft.conflict", "Bản nháp đã thay đổi hoặc trạng thái không cho phép thao tác. Vui lòng tải lại.", 409);
}

