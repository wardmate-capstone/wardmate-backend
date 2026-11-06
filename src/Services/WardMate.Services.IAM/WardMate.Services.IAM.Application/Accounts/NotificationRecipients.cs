namespace WardMate.Services.IAM.Application.Accounts;

public sealed record NotificationRecipientRequest(Guid[] UserIds, string? WardCode, int Page = 1);
public sealed record NotificationRecipient(Guid UserId, string Email, string? WardCode, bool IsOfficer, string? FullName = null);
public interface INotificationRecipients
{
    Task<NotificationRecipient[]> Find(NotificationRecipientRequest request, CancellationToken ct);
}
