using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/applications/{applicationId:guid}/consultations"), Authorize(Policy = "workflow.consultation.read")]
public sealed class ConsultationsController(IConsultationService service) : ReviewControllerBase
{
    [HttpGet("/api/v1/staffing/consultations")]
    public async Task<IActionResult> Inbox(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Respond(await service.Inbox(Actor, page, pageSize, ct));
    [HttpGet("document-access"), Authorize(Policy = "workflow.consultation.respond")]
    public async Task<IActionResult> DocumentAccess(Guid applicationId, CancellationToken ct) => Respond(await service.DocumentAccess(Actor, applicationId, ct));
    [HttpGet]
    public async Task<IActionResult> List(Guid applicationId, CancellationToken ct) => Respond(await service.List(Actor, applicationId, ct));
    [HttpPost, Authorize(Policy = "workflow.dispatch.write")]
    public async Task<IActionResult> Create(Guid applicationId, ConsultationInput input, CancellationToken ct)
    {
        try { return Respond(await service.Create(Actor, applicationId, input, Request.Headers.Authorization.ToString()[7..], ct)); }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        { return Respond(WorkflowResult<object>.Fail(503, "consultation.dependency_unavailable", "Chưa xác minh được cán bộ hoặc lĩnh vực.")); }
    }
    [HttpPost("{id:guid}/answer"), Authorize(Policy = "workflow.consultation.respond")]
    public async Task<IActionResult> Answer(Guid applicationId, Guid id, ConsultationReply input, CancellationToken ct) => Respond(await service.Reply(Actor, applicationId, id, input, false, ct));
    [HttpPost("{id:guid}/close"), Authorize(Policy = "workflow.dispatch.write")]
    public async Task<IActionResult> Close(Guid applicationId, Guid id, ConsultationReply input, CancellationToken ct) => Respond(await service.Reply(Actor, applicationId, id, input, true, ct));
}
