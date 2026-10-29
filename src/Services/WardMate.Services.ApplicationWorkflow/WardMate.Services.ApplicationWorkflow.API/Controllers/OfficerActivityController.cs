using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/officer/activity")]
public sealed class OfficerActivityController(IApplicationReviewStore store) : ReviewControllerBase
{
    private static readonly string[] Actions = ["CLAIM", "RELEASE", "ASSIGN", "TRANSFER", ApplicationStates.Submitted, ApplicationStates.UnderReview,
        ApplicationStates.NeedRevision, ApplicationStates.DocumentsReady, ApplicationStates.OfficiallyReceived, ApplicationStates.Cancelled];
    private const int MaxWindow = 5000;

    /// <summary>Lịch sử thao tác của cán bộ (đổi trạng thái + phân công), mới nhất trước. Manager có thể xem cán bộ trong phường bằng officerId.</summary>
    [HttpGet, Authorize(Policy = "workflow.queue.read"), ProducesResponseType<OfficerActivityPage>(200)]
    public async Task<IActionResult> List([FromQuery] OfficerActivityInput input, CancellationToken ct)
    {
        var actor = Actor;
        var action = string.IsNullOrWhiteSpace(input.Action) ? null : input.Action.Trim().ToUpperInvariant();
        if (input.Page < 1 || input.PageSize is < 1 or > 100 || (long)input.Page * input.PageSize > MaxWindow
            || input.From > input.To || action is not null && !Actions.Contains(action) || input.OfficerId == Guid.Empty)
            return Respond(WorkflowResult<OfficerActivityPage>.Fail(400, "workflow.validation",
                $"Cần page ≥ 1, pageSize 1–100, page × pageSize ≤ {MaxWindow} (thu hẹp from/to để xem cũ hơn), from ≤ to và action hợp lệ."));
        var officerId = input.OfficerId ?? actor.Id;
        if (officerId != actor.Id && !actor.IsManager)
            return Respond(WorkflowResult<OfficerActivityPage>.Fail(403, "workflow.forbidden", "Chỉ Manager được xem lịch sử thao tác của cán bộ khác."));
        if (string.IsNullOrWhiteSpace(actor.WardCode)) return Forbid();
        return Ok(await store.Activity(actor, officerId, input with { Action = action }, ct));
    }
}
