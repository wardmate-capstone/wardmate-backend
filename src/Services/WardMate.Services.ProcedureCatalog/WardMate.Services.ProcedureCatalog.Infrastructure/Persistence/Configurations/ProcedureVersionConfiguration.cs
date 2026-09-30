using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Configurations;

public sealed class ProcedureVersionConfiguration : IEntityTypeConfiguration<ProcedureVersion>
{
    public void Configure(EntityTypeBuilder<ProcedureVersion> builder)
    {
        builder.ToTable("procedure_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DecisionNumber).HasMaxLength(100);
        builder.Property(x => x.EffectiveDate).HasColumnType("date");
        builder.Property(x => x.SnapshotData).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.ProcedureId, x.VersionNumber }).IsUnique();
    }
}
