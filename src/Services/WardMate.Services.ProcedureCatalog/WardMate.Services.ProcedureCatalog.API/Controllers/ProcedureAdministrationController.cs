using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedure-manager"), Authorize(Policy = "ProcedureManager")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ProcedureAdministrationController(ISender sender) : ControllerBase
{
    [HttpGet("procedures/{id:guid}")]
    [ProducesResponseType<ProcedureDetailDto>(200)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Reply(await sender.Send(new ManagerDetailQuery(id), ct));

    [HttpPost("categories")]
    [ProducesResponseType<ProcedureCategoryDto>(201)]
    public async Task<IActionResult> CreateCategory(CategoryInput input, CancellationToken ct)
    {
        var result = await sender.Send(new SaveCategoryCommand(null, input), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : Reply(result);
    }
    [HttpPut("categories/{id:int}")]
    [ProducesResponseType<ProcedureCategoryDto>(200)]
    public async Task<IActionResult> UpdateCategory(int id, CategoryInput input, CancellationToken ct) =>
        Reply(await sender.Send(new SaveCategoryCommand(id, input), ct));
    [HttpDelete("categories/{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteCategoryCommand(id), ct);
        return result.IsSuccess ? NoContent() : Reply(result);
    }
    [HttpDelete("drafts/{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Discard(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DiscardDraftCommand(id), ct);
        return result.IsSuccess ? NoContent() : Reply(result);
    }
    [HttpPost("procedures/{id:guid}/versions/{versionNumber:int}/rollback")]
    [ProducesResponseType<ProcedureDetailDto>(200)]
    public async Task<IActionResult> Rollback(Guid id, int versionNumber, RollbackInput input, CancellationToken ct) =>
        Reply(await sender.Send(new RollbackProcedureCommand(id, versionNumber, input, User.FindFirstValue("sub") ?? "unknown"), ct));
    [HttpGet("procedures/{id:guid}/versions/{versionId:guid}/source")]
    [ProducesResponseType<SourceLinkDto>(200)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Source(Guid id, Guid versionId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Reply(await sender.Send(new VersionSourceQuery(id, versionId), ct));
    }
    [HttpGet("document-forms")]
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
