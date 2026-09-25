using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.Email).HasMaxLength(255).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.HasOne(x => x.Profile).WithOne(x => x.User).HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("user_profiles");
        b.HasKey(x => x.UserId);
        b.Property(x => x.FullName).HasMaxLength(255).IsRequired();
        b.Property(x => x.IdentityNumber).HasMaxLength(20);
        b.HasIndex(x => x.IdentityNumber).IsUnique();
        b.Property(x => x.PhoneNumber).HasMaxLength(20);
        b.Property(x => x.DateOfBirth).HasColumnType("date");
        b.Property(x => x.Gender).HasMaxLength(10);
        b.Property(x => x.PermanentAddress).HasColumnType("text");
        b.Property(x => x.TemporaryAddress).HasColumnType("text");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
    }
}
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.RoleName).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.RoleName).IsUnique();
        b.Property(x => x.Description).HasColumnType("text");
        b.HasData(
            new Role { Id = 1, RoleName = RoleNames.RegisteredCitizen, Description = "Registered citizen" },
            new Role { Id = 2, RoleName = RoleNames.FrontDeskOfficer, Description = "Front desk officer" },
            new Role { Id = 3, RoleName = RoleNames.Manager, Description = "Manager" },
            new Role { Id = 4, RoleName = RoleNames.ProcedureManager, Description = "Procedure catalog manager" },
            new Role { Id = 5, RoleName = RoleNames.ItAdmin, Description = "IT administrator" });
    }
}
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.PermissionCode).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.PermissionCode).IsUnique();
        b.Property(x => x.PermissionName).HasMaxLength(255).IsRequired();
        b.Property(x => x.Module).HasMaxLength(50).IsRequired();
        b.HasData(
            new Permission { Id = 1, PermissionCode = "iam.profile.read", PermissionName = "Read own profile", Module = "IAM" },
            new Permission { Id = 2, PermissionCode = "iam.manage", PermissionName = "Manage identities", Module = "IAM" });
    }
}
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasData(Enumerable.Range(1, 5).Select(id => new RolePermission { RoleId = id, PermissionId = 1 }));
        b.HasData(new RolePermission { RoleId = 5, PermissionId = 2 });
    }
}
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.Token).IsUnique();
        b.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        // UPDATE ... WHERE is_revoked = false makes concurrent refresh single-use.
        b.Property(x => x.IsRevoked).HasDefaultValue(false).IsConcurrencyToken();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
