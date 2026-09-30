using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Configurations;

public sealed class ProcedureConfiguration : IEntityTypeConfiguration<Procedure>
{
    public void Configure(EntityTypeBuilder<Procedure> builder)
    {
        builder.ToTable("procedures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProcedureCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.ProcedureCode).IsUnique().HasMethod("btree");
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IssuingAuthority).HasMaxLength(255);
        builder.Property(x => x.ExecutingAgency).HasColumnType("text");
        builder.Property(x => x.LevelOfImplementation).HasMaxLength(50).HasDefaultValue("Cấp Xã").IsRequired();
        builder.Property(x => x.TargetAudience).HasMaxLength(255).HasDefaultValue("Công dân Việt Nam").IsRequired();
        builder.Property(x => x.FeeSummary).HasMaxLength(255).HasDefaultValue("Miễn phí").IsRequired();
        builder.Property(x => x.ProcessingTimeSummary).HasMaxLength(255).HasDefaultValue("1 ngày").IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.ContentPayload).AsJsonb().IsRequired();
        builder.Property(x => x.ChecklistSchema).AsJsonb().IsRequired(false);
        builder.Property(x => x.FormDefinitions).AsJsonb().IsRequired(false);
        builder.HasIndex(x => x.ContentPayload).HasMethod("gin");
        builder.HasIndex(x => x.ChecklistSchema).HasMethod("gin");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.HasMany(x => x.Versions).WithOne(x => x.Procedure).HasForeignKey(x => x.ProcedureId).OnDelete(DeleteBehavior.Restrict);
    }
}
