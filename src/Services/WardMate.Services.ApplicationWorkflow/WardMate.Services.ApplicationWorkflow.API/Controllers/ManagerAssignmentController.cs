using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/manager/applications")]
public sealed class ManagerAssignmentController(ISender sender, IWorkflowDirectory directory, IApplicationReviewStore store) : ReviewControllerBase
{
    [HttpPut("{id:guid}/assignee"), Authorize(Policy = "workflow.assign.manage"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Assign(Guid id, ManageAssigneeInput input, CancellationToken ct)
    {
        if (!AssignmentHandlers.ValidReason(input.Reason) || input.OfficerId == Guid.Empty || input.ExpectedAssignmentVersion is null or < 0)
            return Respond(WorkflowResult<ReviewResultDto>.Fail(400, "workflow.validation", "Cần lý do 5–500 ký tự, officerId và expectedAssignmentVersion."));
        var actor = Actor;
        var application = await store.Read(actor, id, ct);
        if (application is null || application.UserId == actor.Id || string.IsNullOrWhiteSpace(actor.WardCode) || application.WardCode != actor.WardCode)
            return Respond(WorkflowResult<ReviewResultDto>.Fail(404, "workflow.not_found", "Không tìm thấy hồ sơ trong phường."));
        try
        {
            var target = await directory.Officer(input.OfficerId, Request.Headers.Authorization.ToString()["Bearer ".Length..], ct);
            if (target is null) return Respond(WorkflowResult<ReviewResultDto>.Fail(422, "workflow.invalid_assignee", "Cán bộ đích không hoạt động hoặc không thuộc phạm vi."));
            return Respond(await sender.Send(new ManageAssigneeCommand(actor, id, input, target), ct));
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        { return Respond(WorkflowResult<ReviewResultDto>.Fail(503, "workflow.iam_unavailable", "Chưa xác minh được cán bộ đích. Vui lòng thử lại.")); }
    }
}
