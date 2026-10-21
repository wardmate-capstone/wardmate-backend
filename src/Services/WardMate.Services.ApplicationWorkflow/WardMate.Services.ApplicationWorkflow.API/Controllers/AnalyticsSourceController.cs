using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[ApiController, AllowAnonymous, ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/analytics/v1/daily")]
public sealed class AnalyticsSourceController(IWorkflowAnalyticsSource source, IConfiguration config, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly date, CancellationToken ct)
    {
        var key = config["Analytics:ServiceKey"] ?? "";
        var supplied = Request.Headers["X-Analytics-Key"].ToString();
        if (Encoding.UTF8.GetByteCount(key) < 32 || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            return Problem(statusCode: 401, title: "Không có quyền đọc nguồn thống kê.");
        if (date < new DateOnly(2000, 1, 1) || date > DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime))
            return Problem(statusCode: 400, title: "Ngày thống kê không hợp lệ.");
        try { return Ok(await source.ReadDay(date, ct)); }
        catch (InvalidOperationException ex) when (ex.Message == "analytics.daily_source_limit")
        { return Problem(statusCode: 503, title: "Ngày có quá nhiều sự kiện; cần chia nhỏ nguồn tổng hợp."); }
    }
}
