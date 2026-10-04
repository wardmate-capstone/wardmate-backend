using MediatR;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Queries;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedures")]
public sealed class ProceduresController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ProcedureSummaryDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] int? categoryId = null,
        [FromQuery] string? levelOfImplementation = null, [FromQuery] string sortBy = "Title", [FromQuery] bool isAscending = true,
        [FromQuery] int? page = null, [FromQuery] string? search = null)
    {
        var result = await sender.Send(new GetPublicProceduresPagedQuery(keyword ?? search, categoryId, levelOfImplementation,
            page ?? pageNumber, pageSize, sortBy, isAscending), ct);
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
