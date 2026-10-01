using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Queries;

namespace WardMate.Services.ProcedureCatalog.Application.Interfaces;

public interface IProcedureRepository
{
    Task<Procedure?> GetById(Guid id, CancellationToken ct = default);
    Task<ProcedureListDto> List(GetProceduresQuery query, CancellationToken ct = default);
    void Add(Procedure procedure);
    Task<int> SaveChanges(CancellationToken ct = default);
}
