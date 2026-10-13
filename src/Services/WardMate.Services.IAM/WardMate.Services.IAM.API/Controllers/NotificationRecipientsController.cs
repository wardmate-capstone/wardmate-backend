using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.Application.Accounts;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, AllowAnonymous, ApiExplorerSettings(IgnoreApi = true)]
public sealed class NotificationRecipientsController(INotificationRecipients recipients, IConfiguration configuration) : ControllerBase
{
    [HttpPost("/internal/v1/notification-recipients"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Get(NotificationRecipientRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var expected = configuration["Notifications:ServiceKey"] ?? "";
        if (expected.Length < 32) return Problem(statusCode: 503, title: "Chưa cấu hình khóa dịch vụ thông báo.");
        var supplied = Request.Headers["X-Notification-Service-Key"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            return Unauthorized();
        if (request.UserIds is null || request.UserIds.Length > 10 || request.UserIds.Contains(Guid.Empty) ||
            request.Page is < 1 or > 10000 || request.WardCode is { Length: > 50 })
            return Problem(statusCode: 400, title: "Bộ lọc người nhận không hợp lệ.");
        return Ok(await recipients.Find(request with { WardCode = request.WardCode?.Trim().ToUpperInvariant() }, ct));
    }
}
