using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class ApplicationFormConfiguration : IEntityTypeConfiguration<ApplicationForm>
{
    public void Configure(EntityTypeBuilder<ApplicationForm> builder)
    {
        builder.ToTable("application_forms");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(f => f.ApplicationId)
            .HasColumnName("application_id")
            .IsRequired();

        builder.Property(f => f.FormData)
            .HasColumnName("form_data")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(f => f.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(f => f.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(f => f.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(f => f.DeletedAtUtc)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        builder.Property(f => f.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.Property(f => f.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);
    }
}
