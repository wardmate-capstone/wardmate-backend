using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed record CreateProcedureCommand(ProcedureInput Input) : IRequest<ProcedureResult<ProcedureDetailDto>>;
public sealed record UpdateProcedureCommand(Guid ProcedureId, UpdateProcedureInput Input) : IRequest<ProcedureResult<ProcedureDetailDto>>;
public sealed record ToggleProcedureStatusCommand(Guid ProcedureId, bool IsActive, string? Reason) : IRequest<ProcedureResult<ProcedureStatusDto>>;
public sealed record ProcedureStatusDto(Guid Id, bool IsActive, string? Reason, DateTime UpdatedAt);
