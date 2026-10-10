namespace WardMate.Services.AnalyticsSystem.Application;

public sealed record NotificationDto(Guid Id, Guid RecipientUserId, Guid? ApplicationId,
    string Title, string Content, string Type, string Channel, bool IsRead, DateTime? ReadAt,
    string? ActionUrl, DateTime CreatedAt);

public sealed record CreateNotification(Guid RecipientUserId, Guid? ApplicationId,
    string Title, string Content, string Type, string? ActionUrl = null);

public interface INotificationClient
{
    Task ReceiveNotification(NotificationDto notification);
    Task UpdateUnreadCount(int unreadCount);
}

// Internal application boundary only: never expose arbitrary recipients to a browser.
public interface INotificationService
{
    Task<NotificationDto> CreateAsync(CreateNotification request, CancellationToken ct = default);
    Task<int> UnreadCountAsync(Guid recipient, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid recipient, int page, int pageSize, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid recipient, Guid notificationId, CancellationToken ct = default);
}

public sealed record NotificationAccess(Guid UserId, string? WardCode, string[] Roles);
public interface INotificationDirectory
{
    Task<NotificationAccess?> GetAsync(string accessToken, CancellationToken ct);
}
