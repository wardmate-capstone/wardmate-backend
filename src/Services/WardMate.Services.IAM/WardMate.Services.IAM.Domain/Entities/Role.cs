namespace WardMate.Services.IAM.Domain.Entities;

public sealed class Role
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
