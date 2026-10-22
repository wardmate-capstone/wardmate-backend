namespace WardMate.Services.IAM.Domain.Entities;

public sealed class UserOtp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string? TargetPhoneNumber { get; set; }
    public string OtpHash { get; set; } = "";
    public string Channel { get; set; } = "EMAIL";
    public string Purpose { get; set; } = "FORGOT_PASSWORD";
    public DateTime ExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
public sealed class WelcomeEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
