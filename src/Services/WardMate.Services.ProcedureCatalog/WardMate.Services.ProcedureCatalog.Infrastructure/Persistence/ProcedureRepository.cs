using Microsoft.EntityFrameworkCore;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public sealed class ProcedureRepository(ProcedureDbContext db) : IProcedureRepository
{
    public Task<Procedure?> GetById(Guid id, CancellationToken ct = default) => db.Procedures.AsNoTracking()
        .Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Add(Procedure procedure) => db.Procedures.Add(procedure);
    public Task<int> SaveChanges(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
