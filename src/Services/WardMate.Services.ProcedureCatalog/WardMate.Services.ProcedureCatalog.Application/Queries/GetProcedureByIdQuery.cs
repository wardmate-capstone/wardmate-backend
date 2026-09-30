using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;

namespace WardMate.Services.ProcedureCatalog.Application.Queries;

public sealed record GetProcedureByIdQuery(Guid Id) : IRequest<ProcedureDetailDto?>;
public sealed class GetProcedureByIdHandler(IProcedureRepository repository) : IRequestHandler<GetProcedureByIdQuery, ProcedureDetailDto?>
{
    public async Task<ProcedureDetailDto?> Handle(GetProcedureByIdQuery request, CancellationToken ct)
    {
        var procedure = await repository.GetById(request.Id, ct);
        return procedure is null || !procedure.IsActive ? null : ProcedureDetailDto.From(procedure);
    }
}
