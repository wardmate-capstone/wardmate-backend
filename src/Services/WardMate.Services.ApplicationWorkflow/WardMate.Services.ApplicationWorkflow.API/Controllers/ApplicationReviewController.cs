
using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[ApiController, Authorize, ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401),
 ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
public abstract class ReviewControllerBase : ControllerBase
{
    protected ReviewActor Actor => new(Guid.Parse(User.FindFirstValue("sub")!), User.IsInRole("FRONT_DESK_OFFICER"), User.FindFirstValue("ward_code"), User.IsInRole("MANAGER"));
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
    [HttpGet("{id:guid}"), Authorize(Policy = "workflow.read"), ProducesResponseType<ApplicationDto>(200)]
    public async Task<IActionResult> Detail(Guid id, [FromServices] IApplicationReviewStore store, CancellationToken ct)
    {
        var actor = Actor;
        if (!actor.CanReadWard || string.IsNullOrWhiteSpace(actor.WardCode)) return Forbid();
        var a = await store.Read(actor, id, ct);
        if (a is null || a.UserId == actor.Id || a.WardCode != actor.WardCode)
            return Respond(WorkflowResult<ApplicationDto>.Fail(404, "application.not_found", "Không tìm thấy hồ sơ trong phạm vi phường của bạn."));
        return Ok(ApplicationDto.From(a));
    }
    [HttpGet("pending"), ProducesResponseType<ApplicationPage>(200)]
    [Authorize(Policy = "workflow.queue.read")]
    public async Task<IActionResult> Pending([FromQuery] OfficerPendingInput input, CancellationToken ct) =>
        Ok(await sender.Send(new GetOfficerPendingApplicationsPagedQuery(Actor, input), ct));

    /// <summary>
    /// Danh sách tổng hợp toàn bộ hồ sơ của phường (kể cả đã duyệt / từ chối / cần sửa).
    /// Hỗ trợ lọc: status, procedureId, applicationCode, submittedFrom, submittedTo, page, pageSize.
    /// </summary>
    [HttpGet, ProducesResponseType<ApplicationPage>(200)]
    [Authorize(Policy = "workflow.queue.read")]
    public async Task<IActionResult> List([FromQuery] OfficerListInput input, CancellationToken ct) =>
        Ok(await sender.Send(new GetOfficerApplicationsPagedQuery(Actor, input), ct));

    [HttpPost("{id:guid}/officially-received"), Authorize(Policy = "workflow.receipt.confirm"), RequestSizeLimit(16384)]
    [ProducesResponseType<OfficialReceiptDto>(200)]
    public async Task<IActionResult> Receive(Guid id, OfficialReceiptInput input, CancellationToken ct)
    {
        var result = await sender.Send(new ReceiveApplicationCommand(Actor, id, input), ct);
        if (result.Error is not null) return Respond(result);
        var a = result.Value!.Application;
        return Ok(new OfficialReceiptDto(a.Id, a.Status, a.ReceiptNumber, a.ReceivedAt, a.AppointmentDate));
    }

    [HttpPost("{id:guid}/assign"), ProducesResponseType<ReviewResultDto>(200)]
    [Authorize(Policy = "workflow.assign")]
    public async Task<IActionResult> Assign(Guid id, CancellationToken ct) => Respond(await sender.Send(new AssignOfficerCommand(Actor, id), ct));

    [HttpPost("{id:guid}/comments"), RequestSizeLimit(32 * 1024), ProducesResponseType<ReviewResultDto>(200)]
    [Authorize(Policy = "workflow.comments.write")]
    public async Task<IActionResult> Comment(Guid id, AddCommentInput input, CancellationToken ct) =>
        Respond(await sender.Send(new AddApplicationCommentCommand(Actor, id, input), ct));

    /// <summary>Xác nhận giấy tờ sẵn sàng để mang đến phường; không phê duyệt thủ tục hành chính.</summary>
    [HttpPost("{id:guid}/confirm-readiness"), Authorize(Policy = "workflow.readiness.confirm"), ProducesResponseType<ReviewResultDto>(200)]
    [ProducesResponseType<ProblemDetails>(422), ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> ConfirmReadiness(Guid id, [FromServices] IApplicationReviewStore store,
        [FromServices] IDocumentContentsClient documents, CancellationToken ct)
    {
        var actor = Actor;
        var application = await store.Read(actor, id, ct);
        if (application is null) return Respond(WorkflowResult<ReviewResultDto>.Fail(404, "application.not_found", "Không tìm thấy hồ sơ trong phạm vi."));
        if (!actor.CanMutate || string.IsNullOrWhiteSpace(actor.WardCode) || application.WardCode != actor.WardCode
            || application.AssignedOfficerId != actor.Id || application.UserId == actor.Id) return Forbid();
        if (application.Status != "UNDER_REVIEW") return Respond(WorkflowResult<ReviewResultDto>.Fail(409, "application.invalid_status", "Chỉ xác nhận giấy tờ của hồ sơ đang UNDER_REVIEW."));
        var error = await documents.Validate(id, ApplicationDto.From(application).Checklists.Where(x => x.IsRequired || x.Status == "COMPLETED"), ct);
        if (error is not null) return error;
        return Respond(await sender.Send(new ConfirmDocumentReadinessCommand(actor, id), ct));
    }

    [HttpPost("{id:guid}/request-revision"), RequestSizeLimit(32 * 1024), ProducesResponseType<ReviewResultDto>(200)]
    [Authorize(Policy = "workflow.revision.request")]
    public async Task<IActionResult> RequestRevision(Guid id, RequestRevisionInput input, CancellationToken ct) =>
        Respond(await sender.Send(new RequestRevisionCommand(Actor, id, input), ct));
}

[Route("api/v1/applications"), Tags("Applications")]
public sealed class ApplicationReviewController(ISender sender, IDocumentContentsClient documents,
    IValidator<ResubmitApplicationCommand> resubmitValidator) : ReviewControllerBase
{
    [HttpPost("{id:guid}/resubmit"), RequestSizeLimit(128 * 1024), ProducesResponseType<ReviewResultDto>(200), ProducesResponseType<ProblemDetails>(422)]
    [Authorize(Policy = "workflow.resubmit")]
    public async Task<IActionResult> Resubmit(Guid id, ResubmitInput input, CancellationToken ct)
    {
        // Validate before reading the array or calling dependent services.
        var command = new ResubmitApplicationCommand(Actor.Id, id, input);
        await resubmitValidator.ValidateAndThrowAsync(command, ct);
        var application = await sender.Send(new GetApplicationQuery(Actor.Id, id), ct);
        if (application.Error is not null) return Respond(application);
        var completed = input.Checklists.Where(x => x.Status == "COMPLETED").Select(x => x.Id).ToHashSet();
        var error = await documents.Validate(id, application.Value!.Checklists.Where(x => x.IsRequired || completed.Contains(x.Id)), ct);
        if (error is not null) return error;
        return Respond(await sender.Send(command, ct));
    }

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

