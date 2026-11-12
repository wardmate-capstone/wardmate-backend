using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

/// <summary>MOBILE-01: cấu hình công khai cho app (phiên bản tối thiểu, bảo trì, deep link). Không cần đăng nhập.</summary>
[ApiController, AllowAnonymous, Route("api/v1/system/app-config")]
public sealed class AppConfigController(IConfiguration configuration) : ControllerBase
{
    public sealed record PlatformConfig(string? MinVersion, string? LatestVersion, string? StoreUrl);
    public sealed record AppConfigDto(PlatformConfig Android, PlatformConfig Ios, bool Maintenance, string? MaintenanceMessage,
        string? DeepLinkScheme, string? WebBaseUrl, bool PushEnabled);

    [HttpGet, ProducesResponseType<AppConfigDto>(200)]
    public ActionResult<AppConfigDto> Get()
    {
        PlatformConfig Platform(string name) => new(
            Clean(configuration[$"MobileApp:{name}:MinVersion"]), Clean(configuration[$"MobileApp:{name}:LatestVersion"]),
            Clean(configuration[$"MobileApp:{name}:StoreUrl"]));
        Response.Headers.CacheControl = "public, max-age=300";
        return new AppConfigDto(Platform("Android"), Platform("Ios"), configuration.GetValue<bool>("MobileApp:Maintenance"),
            Clean(configuration["MobileApp:MaintenanceMessage"]), Clean(configuration["MobileApp:DeepLinkScheme"]),
            Clean(configuration["MobileApp:WebBaseUrl"]), configuration.GetValue<bool>("Push:Enabled"));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>PUSH-01: app đăng ký/gỡ token FCM của thiết bị cho tài khoản đang đăng nhập.</summary>
[ApiController, Authorize, Route("api/v1/system/push-devices")]
public sealed class PushDevicesController(PushDevices devices) : ControllerBase
{
    public sealed record UnregisterInput(string? Token);

    private Guid UserId => Guid.TryParse(User.FindFirstValue("sub"), out var id) && id != Guid.Empty
        ? id : throw new PushDeviceException(401, "auth.unauthorized", "Phiên đăng nhập không hợp lệ.");

    /// <summary>Gọi sau khi đăng nhập và mỗi lần FCM cấp token mới. Gọi lại cùng token chỉ cập nhật thời điểm.</summary>
    [HttpPut, RequestSizeLimit(8192), ProducesResponseType(204), ProducesResponseType<ProblemDetails>(400)]
    public async Task<IActionResult> Register(PushDeviceInput input, CancellationToken ct)
    {
        try { await devices.Register(UserId, input, ct); return NoContent(); }
        catch (PushDeviceException e) { return Problem(e); }
    }

    /// <summary>Gọi khi đăng xuất (trước khi xóa access token) để máy không nhận thông báo của tài khoản cũ.</summary>
    [HttpPost("unregister"), RequestSizeLimit(8192), ProducesResponseType(204), ProducesResponseType<ProblemDetails>(400)]
    public async Task<IActionResult> Unregister(UnregisterInput input, CancellationToken ct)
    {
        try { await devices.Unregister(UserId, input.Token, ct); return NoContent(); }
        catch (PushDeviceException e) { return Problem(e); }
    }

    private ObjectResult Problem(PushDeviceException e) => new(new ProblemDetails
    {
        Status = e.Status, Title = e.Message, Instance = Request.Path,
        Extensions = { ["code"] = e.Code, ["traceId"] = HttpContext.TraceIdentifier }
    }) { StatusCode = e.Status, ContentTypes = { "application/problem+json" } };
}
