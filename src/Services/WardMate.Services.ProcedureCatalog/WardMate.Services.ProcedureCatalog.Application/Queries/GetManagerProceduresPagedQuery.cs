using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.Application.Queries;

public sealed record GetManagerProceduresPagedQuery(string? Keyword = null, int? CategoryId = null,
    bool? IsActive = null, int PageNumber = 1, int PageSize = 10, string? LevelOfImplementation = null,
    string SortBy = "Title", bool IsAscending = true) : IRequest<ProcedureResult<PagedResult<ProcedureManagerSummaryDto>>>;

public sealed class GetManagerProceduresPagedHandler(IProcedureRepository repository)
    : IRequestHandler<GetManagerProceduresPagedQuery, ProcedureResult<PagedResult<ProcedureManagerSummaryDto>>>
{
    public async Task<ProcedureResult<PagedResult<ProcedureManagerSummaryDto>>> Handle(GetManagerProceduresPagedQuery request, CancellationToken ct)
    {
        var options = new ProcedureSearchOptions(request.Keyword, request.CategoryId, request.LevelOfImplementation,
            request.IsActive, request.PageNumber, request.PageSize, request.SortBy, request.IsAscending);
        var validation = await new ProcedureSearchValidator().ValidateAsync(options, ct);
        if (!validation.IsValid) return ProcedureResult<PagedResult<ProcedureManagerSummaryDto>>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return ProcedureResult<PagedResult<ProcedureManagerSummaryDto>>.Ok(await repository.ListManager(options, ct));
    }
}
