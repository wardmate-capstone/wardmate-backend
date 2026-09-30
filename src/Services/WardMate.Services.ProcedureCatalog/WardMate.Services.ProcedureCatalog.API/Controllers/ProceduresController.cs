using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Queries;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedures")]
public sealed class ProceduresController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProcedureDetailDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var procedure = await sender.Send(new GetProcedureByIdQuery(id), ct);
        if (procedure is not null) return Ok(procedure);
        return new ObjectResult(new ProblemDetails
        {
            Status = 404, Title = "Không tìm thấy thủ tục đang hoạt động.", Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = "procedure.not_found", ["traceId"] = HttpContext.TraceIdentifier }
        }) { StatusCode = 404, ContentTypes = { "application/problem+json" } };
    }
}
