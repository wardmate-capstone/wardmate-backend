namespace WardMate.Services.IAM.Domain.Entities;

public sealed class Permission
{
    public int Id { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
}
