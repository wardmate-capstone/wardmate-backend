using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

[ApiController, Authorize, AnalyticsAccess("IT_ADMIN"), Route("api/v1/system")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409), ProducesResponseType<ProblemDetails>(503)]
public sealed class SystemAdministrationController(ISystemAdministration service) : ControllerBase
{
    [HttpGet("settings"), ProducesResponseType<SystemSettingDto[]>(200)]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await service.Settings(ct));
    [HttpGet("settings/{key}"), ProducesResponseType<SystemSettingDto>(200)]
    public async Task<IActionResult> Setting(string key, CancellationToken ct) => Ok(await service.Setting(key, ct));
    [HttpPut("settings/{key}"), ProducesResponseType<SystemSettingDto>(200)]
    public async Task<IActionResult> Update(string key, UpdateSystemSetting request, CancellationToken ct)
    {
        var access = (NotificationAccess)HttpContext.Items["analytics.access"]!;
        var metadata = (SystemAuditRequest)HttpContext.Items[typeof(SystemAuditRequest)]!;
        return Ok(await service.Update(key, request, new(access.UserId,
            metadata.ClientIp, metadata.TraceId), ct));
    }
    [HttpGet("audit-logs"), ProducesResponseType<AnalyticsPage<SystemAuditDto>>(200)]
    public async Task<IActionResult> Audits(CancellationToken ct, DateTime? from = null, DateTime? to = null,
        int page = 1, int pageSize = 20) => Ok(await service.Audits(from, to, page, pageSize, ct));
    [HttpGet("audit-logs/{id:guid}"), ProducesResponseType<SystemAuditDto>(200)]
    public async Task<IActionResult> Audit(Guid id, CancellationToken ct) => Ok(await service.Audit(id, ct));
}
