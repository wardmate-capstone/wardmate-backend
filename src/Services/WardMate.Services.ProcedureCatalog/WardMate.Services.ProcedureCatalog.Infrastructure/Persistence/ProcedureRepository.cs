using Microsoft.EntityFrameworkCore;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Queries;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public sealed class ProcedureRepository(ProcedureDbContext db) : IProcedureRepository
{
    public async Task<IReadOnlyList<ProcedureCategoryDto>> ListCategories(CancellationToken ct = default) =>
        await db.ProcedureCategories.AsNoTracking().OrderBy(c => c.CategoryName).ThenBy(c => c.Id)
            .Select(c => new ProcedureCategoryDto(c.Id, c.CategoryName, c.Description)).ToArrayAsync(ct);
    public Task<Procedure?> GetById(Guid id, CancellationToken ct = default) => db.Procedures.AsNoTracking()
        .Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Add(Procedure procedure) => db.Procedures.Add(procedure);
    public async Task<PagedResult<ProcedureSummaryDto>> ListPublic(ProcedureSearchOptions query, CancellationToken ct = default)
    {
        // Defense in depth: public reads cannot opt out of the active-only filter.
        var source = Search(query with { IsActive = true });
        var count = await source.CountAsync(ct);
        var items = await source.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ProcedureSummaryDto(x.Id, x.ProcedureCode, x.Title, x.Category.CategoryName,
                x.LevelOfImplementation, x.FeeSummary, x.ProcessingTimeSummary, x.OriginalPdfUrl, x.UpdatedAt)).ToListAsync(ct);
        return new(items, query.PageNumber, query.PageSize, count);
    }
    public async Task<PagedResult<ProcedureManagerSummaryDto>> ListManager(ProcedureSearchOptions query, CancellationToken ct = default)
    {
        var source = Search(query);
        var count = await source.CountAsync(ct);
        var items = await source.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ProcedureManagerSummaryDto(x.Id, x.ProcedureCode, x.Title, x.Category.CategoryName,
                x.LevelOfImplementation, x.FeeSummary, x.ProcessingTimeSummary, x.OriginalPdfUrl, x.IsActive,
                x.Versions.Count, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
        return new(items, query.PageNumber, query.PageSize, count);
    }
    private IOrderedQueryable<Procedure> Search(ProcedureSearchOptions query)
    {
        var source = db.Procedures.AsNoTracking();
        if (query.IsActive.HasValue) source = source.Where(x => x.IsActive == query.IsActive.Value);
        if (query.CategoryId.HasValue) source = source.Where(x => x.CategoryId == query.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var pattern = "%" + Escape(query.Keyword.Trim()) + "%";
            source = source.Where(x => EF.Functions.ILike(ProcedureDbContext.Unaccent(x.Title), ProcedureDbContext.Unaccent(pattern), "\\") ||
                EF.Functions.ILike(ProcedureDbContext.Unaccent(x.ProcedureCode), ProcedureDbContext.Unaccent(pattern), "\\"));
        }
        if (!string.IsNullOrWhiteSpace(query.LevelOfImplementation))
        {
            var level = Escape(query.LevelOfImplementation.Trim());
            source = source.Where(x => EF.Functions.ILike(ProcedureDbContext.Unaccent(x.LevelOfImplementation), ProcedureDbContext.Unaccent(level), "\\"));
        }
        // Prefer commune/ward entries for default title ordering, without excluding other levels.
        var ordered = source.OrderBy(x => query.SortBy.ToLower() == "title" && string.IsNullOrWhiteSpace(query.LevelOfImplementation) &&
            (ProcedureDbContext.Unaccent(x.LevelOfImplementation).ToLower() == "cap xa" ||
             ProcedureDbContext.Unaccent(x.LevelOfImplementation).ToLower() == "cap phuong") ? 0 : 1);
        ordered = query.SortBy.ToLowerInvariant() switch
        {
            "procedurecode" => query.IsAscending ? ordered.ThenBy(x => x.ProcedureCode) : ordered.ThenByDescending(x => x.ProcedureCode),
            "updatedat" => query.IsAscending ? ordered.ThenBy(x => x.UpdatedAt) : ordered.ThenByDescending(x => x.UpdatedAt),
            "createdat" => query.IsAscending ? ordered.ThenBy(x => x.CreatedAt) : ordered.ThenByDescending(x => x.CreatedAt),
            "levelofimplementation" => query.IsAscending ? ordered.ThenBy(x => x.LevelOfImplementation) : ordered.ThenByDescending(x => x.LevelOfImplementation),
            _ => query.IsAscending ? ordered.ThenBy(x => x.Title) : ordered.ThenByDescending(x => x.Title)
        };
        return ordered.ThenBy(x => x.Id);
    }
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    public Task<int> SaveChanges(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

