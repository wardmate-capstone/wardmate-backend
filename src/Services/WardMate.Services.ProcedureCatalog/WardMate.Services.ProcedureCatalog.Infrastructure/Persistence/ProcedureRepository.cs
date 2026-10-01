using Microsoft.EntityFrameworkCore;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Queries;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public sealed class ProcedureRepository(ProcedureDbContext db) : IProcedureRepository
{
    public Task<Procedure?> GetById(Guid id, CancellationToken ct = default) => db.Procedures.AsNoTracking()
        .Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Add(Procedure procedure) => db.Procedures.Add(procedure);
    public async Task<ProcedureListDto> List(GetProceduresQuery query, CancellationToken ct = default)
    {
        var source = db.Procedures.AsNoTracking();
        if (query.IsActive.HasValue) source = source.Where(x => x.IsActive == query.IsActive.Value);
        if (query.CategoryId.HasValue) source = source.Where(x => x.CategoryId == query.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Treat wildcard characters as literal text; the provider parameterizes the pattern.
            var text = query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{text}%";
            source = source.Where(x => EF.Functions.ILike(x.ProcedureCode, pattern, "\\") || EF.Functions.ILike(x.Title, pattern, "\\"));
        }
        var total = await source.CountAsync(ct);
        var items = await source.OrderBy(x => x.ProcedureCode).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ProcedureListItemDto(x.Id, x.CategoryId, x.Category.CategoryName, x.ProcedureCode,
                x.Title, x.IssuingAuthority, x.ExecutingAgency, x.LevelOfImplementation, x.TargetAudience,
                x.FeeSummary, x.ProcessingTimeSummary, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }
    public Task<int> SaveChanges(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
