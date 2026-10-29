using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/manager/applications"), Authorize(Policy = "workflow.dispatch.read")]
public sealed class ManagerDispatchController(IDispatchService dispatch) : ReviewControllerBase
{
    private string Token => Request.Headers.Authorization.ToString()["Bearer ".Length..];
    private async Task<IActionResult> Safe<T>(Func<Task<WorkflowResult<T>>> action)
    {
        try { return Respond(await action()); }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException || e is OperationCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        { return Respond(WorkflowResult<T>.Fail(503, "dispatch.dependency_unavailable", "Chưa xác minh được dữ liệu IAM/Catalog. Vui lòng thử lại.")); }
    }
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Safe(() => dispatch.List(Actor, page, pageSize, ct));
    [HttpGet("{id:guid}/assignment-candidates")]
    public Task<IActionResult> Candidates(Guid id, CancellationToken ct) => Safe(() => dispatch.Candidates(Actor, id, Token, ct));
    [HttpGet("{id:guid}/assignments")]
    public Task<IActionResult> History(Guid id, CancellationToken ct) => Safe(() => dispatch.History(Actor, id, ct));
    [HttpPost("{id:guid}/assignments"), Authorize(Policy = "workflow.dispatch.write"), RequestSizeLimit(16384)]
    public Task<IActionResult> Assign(Guid id, DispatchAssignmentInput input, CancellationToken ct) => Safe(() => dispatch.Assign(Actor, id, input, Token, ct));
}
