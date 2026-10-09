
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedure-manager/categories"), Authorize(Policy = "procedure.categories.manage")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ProcedureCategoriesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ProcedureCategoryDto>(201)]
    public async Task<IActionResult> CreateCategory(CategoryInput input, CancellationToken ct)
    {
        var result = await sender.Send(new SaveCategoryCommand(null, input), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : Reply(result);
    }
    [HttpPut("{id:int}")]
    [ProducesResponseType<ProcedureCategoryDto>(200)]
    public async Task<IActionResult> UpdateCategory(int id, CategoryInput input, CancellationToken ct) =>
        Reply(await sender.Send(new SaveCategoryCommand(id, input), ct));
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteCategoryCommand(id), ct);
        return result.IsSuccess ? NoContent() : Reply(result);
    }
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

