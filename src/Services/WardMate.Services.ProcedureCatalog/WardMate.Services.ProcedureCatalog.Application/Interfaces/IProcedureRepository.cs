using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Queries;

namespace WardMate.Services.ProcedureCatalog.Application.Interfaces;

public interface IProcedureRepository
{
    Task<IReadOnlyList<ProcedureCategoryDto>> ListCategories(CancellationToken ct = default);
    Task<Procedure?> GetById(Guid id, CancellationToken ct = default);
    Task<PagedResult<ProcedureSummaryDto>> ListPublic(ProcedureSearchOptions query, CancellationToken ct = default);
    Task<PagedResult<ProcedureManagerSummaryDto>> ListManager(ProcedureSearchOptions query, CancellationToken ct = default);
    void Add(Procedure procedure);
    Task<int> SaveChanges(CancellationToken ct = default);
}
