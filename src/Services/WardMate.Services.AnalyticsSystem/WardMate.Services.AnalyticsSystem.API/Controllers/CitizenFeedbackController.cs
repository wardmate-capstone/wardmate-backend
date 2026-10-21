using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

[ApiController, Authorize, AnalyticsAccess("REGISTERED_CITIZEN")]
[Route("api/v1/citizen/feedback/{applicationId:guid}")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class CitizenFeedbackController(IAnalyticsService service) : ControllerBase
{
    private Guid Citizen => ((NotificationAccess)HttpContext.Items["analytics.access"]!).UserId;
    [HttpPost, RequestSizeLimit(16 * 1024), ProducesResponseType<FeedbackDto>(201)]
    public async Task<IActionResult> Create(Guid applicationId, CreateFeedback input, CancellationToken ct)
    {
        if (applicationId == Guid.Empty) throw new AnalyticsException(400, "validation.failed", "ApplicationId không được rỗng.");
        var result = await service.CreateFeedback(Citizen, applicationId, Request.Headers.Authorization.ToString()[7..], input, ct);
        return CreatedAtAction(nameof(Get), new { applicationId }, result);
    }
    [HttpGet, ProducesResponseType<FeedbackDto>(200)]
    public async Task<IActionResult> Get(Guid applicationId, CancellationToken ct) => Ok(await service.GetFeedback(Citizen, applicationId, ct));
}
