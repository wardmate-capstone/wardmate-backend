using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Application.Interfaces;

public interface IProcedureManagementStore
{
    Task<ProcedureCategory?> FindCategory(int id, CancellationToken ct);
    Task<bool> CodeExists(string code, Guid? excludingId, CancellationToken ct);
    Task<Procedure?> LockProcedure(Guid id, CancellationToken ct);
    Task<Procedure?> LockProcedureByCode(string code, CancellationToken ct);
    Task<int> NextVersion(Guid id, CancellationToken ct);
    void Add(Procedure procedure);
    void AddVersion(ProcedureVersion version);
    Task<ProcedureResult<T>> Transaction<T>(Func<Task<ProcedureResult<T>>> action, CancellationToken ct);
    Task<bool> Exists(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProcedureVersion>> ListVersions(Guid id, CancellationToken ct);
}
