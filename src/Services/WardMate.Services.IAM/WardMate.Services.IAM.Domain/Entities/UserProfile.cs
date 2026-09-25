namespace WardMate.Services.IAM.Domain.Entities;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? IdentityNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public string? TemporaryAddress { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}
