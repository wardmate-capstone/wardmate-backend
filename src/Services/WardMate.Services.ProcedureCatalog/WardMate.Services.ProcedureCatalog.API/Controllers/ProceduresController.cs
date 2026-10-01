using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Queries;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedures")]
public sealed class ProceduresController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProcedureListDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] int? categoryId = null)
    {
        var result = await sender.Send(new GetProceduresQuery(page, pageSize, search, categoryId, IsActive: true), ct);
        if (result.IsSuccess) return Ok(result.Value);
        var error = result.Error!;
        return new ObjectResult(new ProblemDetails
        {
            Status = error.Status, Title = error.Message, Instance = Request.Path,
            Extensions = { ["code"] = error.Code, ["errors"] = error.Errors, ["traceId"] = HttpContext.TraceIdentifier }
        }) { StatusCode = error.Status, ContentTypes = { "application/problem+json" } };
    }

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
