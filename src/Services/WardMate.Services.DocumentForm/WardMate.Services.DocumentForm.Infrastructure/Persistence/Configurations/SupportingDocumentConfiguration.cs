using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class SupportingDocumentConfiguration : IEntityTypeConfiguration<SupportingDocument>
{
    public void Configure(EntityTypeBuilder<SupportingDocument> builder)
    {
        builder.ToTable("supporting_documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(d => d.ApplicationId)
            .HasColumnName("application_id")
            .IsRequired();

        builder.Property(d => d.ChecklistId)
            .HasColumnName("checklist_id");

        builder.Property(d => d.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.BlobUrl)
            .HasColumnName("blob_url")
            .IsRequired();

        builder.Property(d => d.FileSizeBytes)
            .HasColumnName("file_size_bytes");

        builder.Property(d => d.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100);

        builder.Property(d => d.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(d => d.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(d => d.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(d => d.DeletedAtUtc)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        builder.Property(d => d.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.Property(d => d.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);
    }
}
