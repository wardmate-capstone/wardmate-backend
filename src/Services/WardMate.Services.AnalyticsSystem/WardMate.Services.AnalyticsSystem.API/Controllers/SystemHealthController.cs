using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

[ApiController, Authorize, AnalyticsAccess("IT_ADMIN"), Route("api/v1/system/health")]
public sealed class SystemHealthController(ISystemHealth health) : ControllerBase
{
    [HttpPost("check-now")]
    public async Task<IActionResult> CheckNow(CancellationToken ct)
    {
        var access = (NotificationAccess)HttpContext.Items["analytics.access"]!;
        return Accepted(new { requestId = await health.RequestCheck(access.UserId, ct), status = "Queued" });
    }
    [HttpGet("services")]
    public async Task<IActionResult> Services(CancellationToken ct) => Ok(await health.Latest(ct));

    [HttpGet("services/{serviceName}")]
    public async Task<IActionResult> Service(string serviceName, CancellationToken ct)
    {
        var row = (await health.Latest(ct)).SingleOrDefault(x => x.ServiceName == serviceName);
        if (row is null) throw new AnalyticsException(404, "system.service_not_found", "Service chưa được cấu hình.");
        return Ok(row);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct, string? service = null,
        DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20)
        => Ok(await health.History(service, from, to, page, pageSize, ct));
}
