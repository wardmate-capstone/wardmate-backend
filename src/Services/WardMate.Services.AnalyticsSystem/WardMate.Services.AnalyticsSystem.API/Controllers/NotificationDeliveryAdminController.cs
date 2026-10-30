using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

[ApiController, Authorize, AnalyticsAccess("IT_ADMIN"), Route("api/v1/admin/notification-deliveries")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class NotificationDeliveryAdminController(IDeliveryAdministration service) : ControllerBase
{
    private Guid Actor(bool write = false)
    {
        var access = (NotificationAccess)HttpContext.Items["analytics.access"]!;
        if (access.Permissions?.Contains("notifications.deliveries.read") != true
            || write && !access.Permissions.Contains("notifications.deliveries.manage"))
            throw new AnalyticsException(403,"notifications.forbidden","Thiếu quyền quản trị delivery.");
        Response.Headers.CacheControl = "no-store";
        return access.UserId;
    }
    [HttpGet, ProducesResponseType<DeliveryPage>(200)]
    public async Task<IActionResult> List([FromQuery] DeliveryQuery query,CancellationToken ct)
    { Actor(); return Ok(await service.List(query,ct)); }
    [HttpGet("{id:guid}"), ProducesResponseType<DeliveryAdminDto>(200)]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)
    { Actor(); return Ok(await service.Get(id,ct)); }
    [HttpGet("{id:guid}/audit"), ProducesResponseType<DeliveryAuditDto[]>(200)]
    public async Task<IActionResult> Audit(Guid id,CancellationToken ct,int page=1,int pageSize=20)
    { Actor(); return Ok(await service.Audit(id,page,pageSize,ct)); }
    [HttpPost("{id:guid}/replay"), RequestSizeLimit(4096), ProducesResponseType<DeliveryAdminDto>(202)]
    public async Task<IActionResult> Replay(Guid id,DeliveryReplayInput input,CancellationToken ct)
    { var row=await service.Replay(Actor(true),id,input,ct); return Accepted($"/api/v1/admin/notification-deliveries/{id}",row); }
}
