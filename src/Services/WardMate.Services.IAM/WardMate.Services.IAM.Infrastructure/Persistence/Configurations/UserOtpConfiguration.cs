using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.IAM.Domain.Entities;
namespace WardMate.Services.IAM.Infrastructure.Persistence.Configurations;

public sealed class UserOtpConfiguration : IEntityTypeConfiguration<UserOtp>
{
    public void Configure(EntityTypeBuilder<UserOtp> b)
    {
        b.ToTable("user_otps", "iam", t =>
        {
            t.HasCheckConstraint("ck_user_otps_attempts", "attempts BETWEEN 0 AND 5");
            t.HasCheckConstraint("ck_user_otps_channel", "channel IN ('EMAIL','SMS')");
            t.HasCheckConstraint("ck_user_otps_purpose", "purpose IN ('CHANGE_PASSWORD','FORGOT_PASSWORD')");
        });
        b.HasKey(x => x.Id); b.Property(x => x.OtpHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(10).IsRequired(); b.Property(x => x.Purpose).HasMaxLength(30).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}
public sealed class WelcomeEmailConfiguration : IEntityTypeConfiguration<WelcomeEmail>
{
    public void Configure(EntityTypeBuilder<WelcomeEmail> b)
    {
        b.ToTable("welcome_emails", "iam"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.UserId).IsUnique(); b.HasIndex(x => new { x.SentAtUtc, x.NextAttemptAtUtc });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
