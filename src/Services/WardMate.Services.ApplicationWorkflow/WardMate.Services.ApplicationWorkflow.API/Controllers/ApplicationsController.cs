using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[ApiController, Authorize, Route("api/v1/applications")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ApplicationsController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpPost, RequestSizeLimit(128 * 1024)]
    [ProducesResponseType<ApplicationDto>(201)]
    public async Task<IActionResult> Create(CreateApplicationInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateApplicationCommand(Actor, input), ct);
        return result.Error is null ? CreatedAtAction("Get", new { id = result.Value!.Id }, result.Value) : Respond(result);
    }

    private IActionResult Respond(WorkflowResult<ApplicationDto> result)
    {
        if (result.Error is null) return Ok(result.Value);
        var error = result.Error;
        var problem = new ProblemDetails { Status = error.Status, Title = error.Message, Instance = Request.Path,
            Extensions = { ["code"] = error.Code, ["traceId"] = HttpContext.TraceIdentifier } };
        if (error.Details is not null) problem.Extensions["missingItems"] = error.Details;
        return new ObjectResult(problem) { StatusCode = error.Status, ContentTypes = { "application/problem+json" } };
    }
}
