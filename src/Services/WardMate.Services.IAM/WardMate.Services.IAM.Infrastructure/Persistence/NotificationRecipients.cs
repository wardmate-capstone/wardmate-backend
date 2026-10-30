using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Accounts;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class NotificationRecipients(IamDbContext db) : INotificationRecipients
{
    public Task<NotificationRecipient[]> Find(NotificationRecipientRequest request, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(u => u.IsActive && (request.UserIds.Contains(u.Id) || request.WardCode != null && u.Ward != null && u.Ward.Code == request.WardCode &&
            u.UserRoles.Any(r => r.Role.RoleName == "FRONT_DESK_OFFICER") &&
            u.UserRoles.Any(r => r.Role.RolePermissions.Any(p => p.Permission.PermissionCode == "workflow.queue.read"))))
        .OrderBy(u => u.Id).Skip((request.Page - 1) * 200).Take(200)
        .Select(u => new NotificationRecipient(u.Id, u.Email,
            u.Ward == null ? null : u.Ward.Code, u.UserRoles.Any(r => r.Role.RoleName == "FRONT_DESK_OFFICER") &&
            u.UserRoles.Any(r => r.Role.RolePermissions.Any(p => p.Permission.PermissionCode == "workflow.queue.read")), u.Profile == null ? null : u.Profile.FullName))
        .ToArrayAsync(ct);
}
