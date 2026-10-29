using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/staffing"), Authorize(Policy = "workflow.staffing.read")]
public sealed class StaffingController(IStaffingService service) : ReviewControllerBase
{
    private async Task<IActionResult> Safe<T>(Func<Task<WorkflowResult<T>>> action)
    {
        try { return Respond(await action()); }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException || e is OperationCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        { return Respond(WorkflowResult<T>.Fail(503, "staffing.iam_unavailable", "Chưa xác minh được cán bộ từ IAM.")); }
    }
    [HttpGet("shifts")]
    public Task<IActionResult> Shifts([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct) => Safe(() => service.Shifts(Actor, from, to, ct));
    [HttpPost("shifts"), Authorize(Policy = "workflow.staffing.manage")]
    public Task<IActionResult> Create(ShiftInput input, CancellationToken ct) => Safe(() => service.CreateShift(Actor, input, Request.Headers.Authorization.ToString()[7..], ct));
    [HttpPost("shifts/{id:guid}/cancel"), Authorize(Policy = "workflow.staffing.manage")]
    public Task<IActionResult> Cancel(Guid id, StaffingReason input, CancellationToken ct) => Safe(() => service.CancelShift(Actor, id, input, ct));
    [HttpGet("leaves")]
    public Task<IActionResult> Leaves([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct) => Safe(() => service.Leaves(Actor, from, to, ct));
    [HttpPost("leaves"), Authorize(Policy = "workflow.leave.request")]
    public Task<IActionResult> RequestLeave(LeaveInput input, CancellationToken ct) => Safe(() => service.RequestLeave(Actor, input, ct));
    [HttpPost("leaves/{id:guid}/decision"), Authorize(Policy = "workflow.staffing.decide")]
    public Task<IActionResult> Decide(Guid id, LeaveDecisionInput input, CancellationToken ct) => Safe(() => service.DecideLeave(Actor, id, input, ct));
    [HttpGet("leaves/{id:guid}/impact"), Authorize(Policy = "workflow.staffing.manage")]
    public Task<IActionResult> Impact(Guid id, CancellationToken ct) => Safe(() => service.Impact(Actor, id, ct));
    [HttpGet("leaves/{id:guid}/history")]
    public Task<IActionResult> History(Guid id, CancellationToken ct) => Safe(() => service.LeaveHistory(Actor, id, ct));
}
