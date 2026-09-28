using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence.Configurations;

public sealed class RbacAuditConfiguration : IEntityTypeConfiguration<RbacAuditLog>
{
    public void Configure(EntityTypeBuilder<RbacAuditLog> b)
    {
        b.ToTable("rbac_audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.Details).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.CreatedAt, x.Id });
        // Deliberately no cascading FKs: audit history survives deletion of its target role.
    }
}
