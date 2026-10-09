using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[ApiController, Authorize, ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401),
 ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public abstract class ReviewControllerBase : ControllerBase
{
    protected ReviewActor Actor => new(Guid.Parse(User.FindFirstValue("sub")!), User.IsInRole("FRONT_DESK_OFFICER"), User.FindFirstValue("ward_code"));
    protected IActionResult Respond<T>(WorkflowResult<T> result)
    {
        if (result.Error is null) return Ok(result.Value);
        var e = result.Error;
        var problem = new ProblemDetails { Status = e.Status, Title = e.Message, Instance = Request.Path,
            Extensions = { ["code"] = e.Code, ["traceId"] = HttpContext.TraceIdentifier } };
        if (e.Details is not null) problem.Extensions["missingItems"] = e.Details;
        return new ObjectResult(problem) { StatusCode = e.Status, ContentTypes = { "application/problem+json" } };
    }
}

[Route("api/v1/officer/applications")]
public sealed class OfficerApplicationController(ISender sender) : ReviewControllerBase
{
    [HttpGet("pending"), ProducesResponseType<ApplicationPage>(200)]
    [Authorize(Policy = "workflow.queue.read")]
    public async Task<IActionResult> Pending([FromQuery] OfficerPendingInput input, CancellationToken ct) =>
        Ok(await sender.Send(new GetOfficerPendingApplicationsPagedQuery(Actor, input), ct));

    [HttpPost("{id:guid}/assign"), ProducesResponseType<ReviewResultDto>(200)]
    [Authorize(Policy = "workflow.assign")]
    public async Task<IActionResult> Assign(Guid id, CancellationToken ct) => Respond(await sender.Send(new AssignOfficerCommand(Actor, id), ct));

    [HttpPost("{id:guid}/comments"), RequestSizeLimit(32 * 1024), ProducesResponseType<ReviewResultDto>(200)]
    [Authorize(Policy = "workflow.comments.write")]
    public async Task<IActionResult> Comment(Guid id, AddCommentInput input, CancellationToken ct) =>
        Respond(await sender.Send(new AddApplicationCommentCommand(Actor, id, input), ct));

    [HttpPost("{id:guid}/approve"), Authorize(Policy = "workflow.approve"), ProducesResponseType<ReviewResultDto>(200)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Respond(await sender.Send(new ApproveApplicationCommand(Actor, id), ct));

    public sealed record RejectInput(string Reason);
    [HttpPost("{id:guid}/reject"), Authorize(Policy = "workflow.reject"), RequestSizeLimit(32 * 1024), ProducesResponseType<ReviewResultDto>(200)]
    public async Task<IActionResult> Reject(Guid id, RejectInput input, CancellationToken ct) => Respond(await sender.Send(new RejectApplicationCommand(Actor, id, input.Reason), ct));
    [HttpPost("{id:guid}/request-revision"), RequestSizeLimit(32 * 1024), ProducesResponseType<ReviewResultDto>(200)]
    [Authorize(Policy = "workflow.revision.request")]
    public async Task<IActionResult> RequestRevision(Guid id, RequestRevisionInput input, CancellationToken ct) =>
        Respond(await sender.Send(new RequestRevisionCommand(Actor, id, input), ct));
}

[Route("api/v1/applications"), Tags("Applications")]
public sealed class ApplicationReviewController(ISender sender) : ReviewControllerBase
{
    [HttpPost("{id:guid}/resubmit"), RequestSizeLimit(128 * 1024), ProducesResponseType<ReviewResultDto>(200), ProducesResponseType<ProblemDetails>(422)]
    [Authorize(Policy = "workflow.resubmit")]
    public async Task<IActionResult> Resubmit(Guid id, ResubmitInput input, CancellationToken ct) =>
        Respond(await sender.Send(new ResubmitApplicationCommand(Actor.Id, id, input), ct));

    [HttpGet("{id:guid}/comments"), ProducesResponseType<CommentDto[]>(200)]
    [Authorize(Policy = "workflow.comments.read")]
    public async Task<IActionResult> Comments(Guid id, [FromQuery] int? versionNumber, [FromQuery] string? status, CancellationToken ct) =>
        Respond(await sender.Send(new GetApplicationCommentsQuery(Actor, id, versionNumber, status), ct));

    [HttpGet("{id:guid}/versions"), ProducesResponseType<VersionDto[]>(200)]
    [Authorize(Policy = "workflow.versions.read")]
    public async Task<IActionResult> Versions(Guid id, CancellationToken ct) => Respond(await sender.Send(new GetApplicationVersionsQuery(Actor, id), ct));

    [HttpGet("{id:guid}/diff"), ProducesResponseType<FieldDiffDto[]>(200)]
    [Authorize(Policy = "workflow.diff.read")]
    public async Task<IActionResult> Diff(Guid id, [FromQuery] int fromVersion, [FromQuery] int toVersion, CancellationToken ct) =>
        Respond(await sender.Send(new CompareApplicationVersionsQuery(Actor, id, fromVersion, toVersion), ct));
}

