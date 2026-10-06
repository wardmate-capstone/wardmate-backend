using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;

namespace WardMate.Services.ProcedureCatalog.Application.Queries;

public sealed record GetProcedureCategoriesQuery : IRequest<IReadOnlyList<ProcedureCategoryDto>>;

public sealed class GetProcedureCategoriesHandler(IProcedureRepository repository)
    : IRequestHandler<GetProcedureCategoriesQuery, IReadOnlyList<ProcedureCategoryDto>>
{
    public Task<IReadOnlyList<ProcedureCategoryDto>> Handle(GetProcedureCategoriesQuery request, CancellationToken ct) =>
        repository.ListCategories(ct);
}
