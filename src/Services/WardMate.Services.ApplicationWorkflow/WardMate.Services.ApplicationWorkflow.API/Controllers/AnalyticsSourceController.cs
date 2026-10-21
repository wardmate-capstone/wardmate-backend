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
    [HttpGet("/internal/analytics/v1/deadlines")]
    public async Task<IActionResult> Deadlines([FromQuery] string ward, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? procedureId, CancellationToken ct)
    {
        var key = config["Analytics:ServiceKey"] ?? "";
        var supplied = Request.Headers["X-Analytics-Key"].ToString();
        if (Encoding.UTF8.GetByteCount(key) < 32 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            return Problem(statusCode: 401, title: "Không có quyền đọc nguồn thống kê.");
        if (string.IsNullOrWhiteSpace(ward) || ward.Length > 50 || from < new DateOnly(2000, 1, 1) || from > to || to.DayNumber - from.DayNumber >= 366
            || to > DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime)) return Problem(statusCode: 400, title: "Phạm vi thống kê không hợp lệ.");
        try { return Ok(await source.ReadDeadlines(ward, from, to, procedureId, ct)); }
        catch (InvalidOperationException e) when (e.Message == "analytics.daily_source_limit")
        { return Problem(statusCode: 503, title: "Nguồn deadline vượt giới hạn, cần thu hẹp thủ tục."); }
    }
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
