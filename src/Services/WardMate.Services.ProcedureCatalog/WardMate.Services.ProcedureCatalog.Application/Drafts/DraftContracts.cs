using System.Text.Json;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.Application.Drafts;

public sealed record DraftDto(Guid Id, string Status, string PdfFileName, JsonElement Payload,
    JsonElement Warnings, string ExtractedText, string? FailureCode, Guid Revision,
    Guid? PublishedProcedureId, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record SaveDraftInput(Guid Revision, JsonElement Payload);
public sealed record DraftSummaryDto(Guid Id, string Status, string PdfFileName, string? FailureCode,
    Guid Revision, Guid? PublishedProcedureId, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record ConfirmDraftInput(Guid Revision, bool Confirmed);
public sealed record ExtractionResult(JsonElement Payload, string ExtractedText, string[] Warnings);

public interface IProcedureDraftService
{
    Task<ProcedureResult<DraftDto>> Upload(Stream pdf, string fileName, string actor, CancellationToken ct);
    Task<ProcedureResult<DraftDto>> Get(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DraftSummaryDto>> List(int page, int pageSize, CancellationToken ct);
    Task<ProcedureResult<DraftDto>> Save(Guid id, SaveDraftInput input, CancellationToken ct);
    Task<ProcedureResult<DraftDto>> Retry(Guid id, Guid revision, CancellationToken ct);
    Task<ProcedureResult<string>> ReadUrl(Guid id, CancellationToken ct);
    Task<ProcedureResult<string>> PublishedReadUrl(Guid procedureId, CancellationToken ct);
    Task<ProcedureResult<ProcedureDetailDto>> Publish(Guid id, ConfirmDraftInput input, string actor, CancellationToken ct);
}

public interface IProcedureExtractor
{
    Task<ExtractionResult> Extract(Stream pdf, CancellationToken ct);
}
