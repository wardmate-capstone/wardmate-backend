using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

[Authorize]
public sealed class NotificationHub(INotificationService notifications, INotificationDirectory directory) : Hub<INotificationClient>
{
    public static string UserGroup(Guid id) => $"user_{id:D}";
    public static string WardGroup(string code) => $"ward_{code.Trim().ToUpperInvariant()}";

    private Guid Recipient => Guid.TryParse(Context.User?.FindFirst("sub")?.Value
        ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id != Guid.Empty
            ? id : throw new HubException("Phiên đăng nhập không hợp lệ.");

    public override async Task OnConnectedAsync()
    {
        try
        {
            var token = await Context.GetHttpContext()!.GetTokenAsync("access_token");
            var access = string.IsNullOrEmpty(token) ? null : await directory.GetAsync(token, Context.ConnectionAborted);
            if (access is null || access.UserId != Recipient || access.Roles is null)
                throw new HubException("Tài khoản không còn quyền kết nối.");
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(Recipient), Context.ConnectionAborted);
            // IAM currently does not issue a ward claim; use the authenticated IAM access context.
            if (access.Roles.Any(x => x is "FRONT_DESK_OFFICER" or "MANAGER") && !string.IsNullOrWhiteSpace(access.WardCode))
                await Groups.AddToGroupAsync(Context.ConnectionId, WardGroup(access.WardCode), Context.ConnectionAborted);
            await Clients.Caller.UpdateUnreadCount(await notifications.UnreadCountAsync(Recipient, Context.ConnectionAborted));
            await base.OnConnectedAsync();
        }
        catch
        {
            Context.Abort();
            throw new HubException("Không thể kết nối thông báo. Vui lòng đăng nhập lại hoặc thử lại sau.");
        }
    }

    public Task<IReadOnlyList<NotificationDto>> GetNotifications(int page = 1, int pageSize = 20)
    {
        if (page < 1 || page > 1_000_000 || pageSize is < 1 or > 100)
            throw new HubException("Trang phải từ 1 đến 1000000; số thông báo mỗi trang từ 1 đến 100.");
        return notifications.ListAsync(Recipient, page, pageSize, Context.ConnectionAborted);
    }

    public Task<int> GetUnreadCount() => notifications.UnreadCountAsync(Recipient, Context.ConnectionAborted);

    public async Task MarkRead(Guid notificationId)
    {
        if (!await notifications.MarkReadAsync(Recipient, notificationId, Context.ConnectionAborted))
            throw new HubException("Không tìm thấy thông báo của bạn.");
    }
    // SignalR removes disconnected connections from every group automatically.
}
