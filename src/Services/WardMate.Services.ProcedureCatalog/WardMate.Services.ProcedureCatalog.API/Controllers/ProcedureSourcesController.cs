using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ProcedureCatalog.Application.Drafts;

namespace WardMate.Services.ProcedureCatalog.API.Controllers;

[ApiController, Route("api/v1/procedures")]
public sealed class ProcedureSourcesController(IProcedureDraftService drafts) : ControllerBase
{
    [HttpGet("{id:guid}/source")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await drafts.PublishedReadUrl(id, ct);
        if (result.IsSuccess) return Ok(new { url = result.Value, expiresInSeconds = 600 });
        var error = result.Error!;
        return new ObjectResult(new ProblemDetails { Status = error.Status, Title = error.Message, Instance = Request.Path,
            Extensions = { ["code"] = error.Code, ["traceId"] = HttpContext.TraceIdentifier } })
        { StatusCode = error.Status, ContentTypes = { "application/problem+json" } };
    }
}
