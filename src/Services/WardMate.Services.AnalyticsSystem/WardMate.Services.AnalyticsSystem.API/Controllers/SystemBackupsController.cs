using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

[ApiController, Authorize, AnalyticsAccess("IT_ADMIN"), Route("api/v1/system/backups")]
public sealed class SystemBackupsController(ISystemBackups backups) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, string? service = null, string? status = null,
        DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20)
        => Ok(await backups.List(service, status, from, to, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await backups.Get(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateBackupRequest request, CancellationToken ct)
    {
        var access = (NotificationAccess)HttpContext.Items["analytics.access"]!;
        var result = await backups.Create(request, access.UserId, ct);
        return AcceptedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        var access = (NotificationAccess)HttpContext.Items["analytics.access"]!;
        var result = await backups.Verify(id, access.UserId, ct);
        return Ok(result);
    }
}
