
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedure-manager/document-forms"), Authorize(Policy = "ProcedureManager")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ProcedureDocumentFormsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DocumentFormOptionDto[]>(200)]
    [ProducesResponseType<ProblemDetails>(502)]
    [ProducesResponseType<ProblemDetails>(503)]
    [ProducesResponseType<ProblemDetails>(504)]
    public async Task<IActionResult> Forms(CancellationToken ct, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] string? searchCode = null) =>
        Reply(await sender.Send(new DocumentFormsQuery(page, pageSize, searchCode), ct));
    private IActionResult Reply<T>(ProcedureResult<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        var e = result.Error!;
        var problem = new ProblemDetails { Status = e.Status, Title = e.Message, Instance = Request.Path };
        problem.Extensions["code"] = e.Code; problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        if (e.Errors is not null) problem.Extensions["errors"] = e.Errors;
        return new ObjectResult(problem) { StatusCode = e.Status, ContentTypes = { "application/problem+json" } };
    }
}
