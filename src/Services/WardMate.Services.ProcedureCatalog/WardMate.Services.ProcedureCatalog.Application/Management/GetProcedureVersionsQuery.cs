using System.Text.Json;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed record ProcedureVersionDto(Guid Id, int VersionNumber, string? DecisionNumber,
    DateOnly EffectiveDate, JsonElement SnapshotData, DateTime CreatedAt);
public sealed record GetProcedureVersionsQuery(Guid ProcedureId) : IRequest<ProcedureResult<IReadOnlyList<ProcedureVersionDto>>>;

public sealed class GetProcedureVersionsQueryHandler(IProcedureManagementStore store)
    : IRequestHandler<GetProcedureVersionsQuery, ProcedureResult<IReadOnlyList<ProcedureVersionDto>>>
{
    public async Task<ProcedureResult<IReadOnlyList<ProcedureVersionDto>>> Handle(GetProcedureVersionsQuery request, CancellationToken ct)
    {
        if (!await store.Exists(request.ProcedureId, ct)) return ProcedureResult<IReadOnlyList<ProcedureVersionDto>>.NotFound();
        var versions = await store.ListVersions(request.ProcedureId, ct);
        return ProcedureResult<IReadOnlyList<ProcedureVersionDto>>.Ok(versions.Select(x => new ProcedureVersionDto(
            x.Id, x.VersionNumber, x.DecisionNumber, x.EffectiveDate, JsonSerializer.Deserialize<JsonElement>(x.SnapshotData), x.CreatedAt)).ToList());
    }
}
