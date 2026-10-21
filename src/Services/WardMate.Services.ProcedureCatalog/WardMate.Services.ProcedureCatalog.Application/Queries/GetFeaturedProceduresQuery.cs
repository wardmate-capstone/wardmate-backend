using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
namespace WardMate.Services.ProcedureCatalog.Application.Queries;
public sealed record GetFeaturedProceduresQuery : IRequest<IReadOnlyList<ProcedureSummaryDto>>;
public sealed class GetFeaturedProceduresHandler(IProcedureRepository repository) : IRequestHandler<GetFeaturedProceduresQuery, IReadOnlyList<ProcedureSummaryDto>>
{
    public Task<IReadOnlyList<ProcedureSummaryDto>> Handle(GetFeaturedProceduresQuery request, CancellationToken ct) => repository.Featured(ct);
}
