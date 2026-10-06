namespace WardMate.Services.IAM.Domain.Entities;

public sealed class Ward
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
