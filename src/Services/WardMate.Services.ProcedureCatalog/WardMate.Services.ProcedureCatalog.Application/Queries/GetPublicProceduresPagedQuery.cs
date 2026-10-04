using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.Application.Queries;

public sealed record GetPublicProceduresPagedQuery(string? Keyword = null, int? CategoryId = null,
    string? LevelOfImplementation = null, int PageNumber = 1, int PageSize = 10,
    string SortBy = "Title", bool IsAscending = true) : IRequest<ProcedureResult<PagedResult<ProcedureSummaryDto>>>;

public sealed class GetPublicProceduresPagedHandler(IProcedureRepository repository)
    : IRequestHandler<GetPublicProceduresPagedQuery, ProcedureResult<PagedResult<ProcedureSummaryDto>>>
{
    public async Task<ProcedureResult<PagedResult<ProcedureSummaryDto>>> Handle(GetPublicProceduresPagedQuery request, CancellationToken ct)
    {
        var options = new ProcedureSearchOptions(request.Keyword, request.CategoryId, request.LevelOfImplementation,
            true, request.PageNumber, request.PageSize, request.SortBy, request.IsAscending);
        var validation = await new ProcedureSearchValidator().ValidateAsync(options, ct);
        if (request.PageSize > 50) validation.Errors.Add(new("PageSize", "Kích thước trang công khai phải từ 1 đến 50."));
        if (!validation.IsValid) return ProcedureResult<PagedResult<ProcedureSummaryDto>>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return ProcedureResult<PagedResult<ProcedureSummaryDto>>.Ok(await repository.ListPublic(options, ct));
    }
}
