using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Application.Interfaces;

public interface IProcedureRepository
{
    Task<Procedure?> GetById(Guid id, CancellationToken ct = default);
    void Add(Procedure procedure);
    Task<int> SaveChanges(CancellationToken ct = default);
}
