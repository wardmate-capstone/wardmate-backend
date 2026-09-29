using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class FormTemplateVersionConfiguration : IEntityTypeConfiguration<FormTemplateVersion>
{
    public void Configure(EntityTypeBuilder<FormTemplateVersion> builder)
    {
        builder.ToTable("form_template_versions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(v => v.TemplateId)
            .HasColumnName("template_id")
            .IsRequired();

        builder.Property(v => v.VersionNumber)
            .HasColumnName("version_number")
            .IsRequired();

        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber }).IsUnique();

        builder.Property(v => v.SchemaDefinition)
            .HasColumnName("schema_definition")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(v => v.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(v => v.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(v => v.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(v => v.DeletedAtUtc)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        builder.Property(v => v.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.Property(v => v.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);
    }
}
