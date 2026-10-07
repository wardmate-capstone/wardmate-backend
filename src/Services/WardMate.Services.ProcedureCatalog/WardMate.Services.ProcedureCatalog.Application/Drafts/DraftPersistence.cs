using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Application.Drafts;

public sealed record DraftProcessingOptions(bool ExtractionEnabled);
public interface IDraftFileStorage
{
    bool IsConfigured { get; }
    Task<string> Upload(Stream pdf, string blobName, CancellationToken ct);
    Task<string> ReadUrl(string blobName, CancellationToken ct);
}
public interface IDraftPersistence
{
    Task<ProcedureDraft?> Find(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DraftSummaryDto>> List(int page, int pageSize, CancellationToken ct);
    Task Insert(ProcedureDraft draft, CancellationToken ct);
    Task<bool> Save(CancellationToken ct);
    Task<Guid?> FindPublishedDraftId(Guid procedureId, CancellationToken ct);
    Task<ProcedureResult<ProcedureDetailDto>> PublishLocked(Guid id,
        Func<ProcedureDraft?, Task<ProcedureResult<ProcedureDetailDto>>> action, CancellationToken ct);
}
