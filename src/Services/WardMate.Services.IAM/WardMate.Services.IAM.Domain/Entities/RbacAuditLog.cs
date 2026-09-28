namespace WardMate.Services.IAM.Domain.Entities;

public sealed class RbacAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? TargetUserId { get; set; }
    public int? RoleId { get; set; }
    public int? PermissionId { get; set; }
    public string Details { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
