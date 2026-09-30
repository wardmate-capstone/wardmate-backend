using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class GeneratedDocumentConfiguration : IEntityTypeConfiguration<GeneratedDocument>
{
    public void Configure(EntityTypeBuilder<GeneratedDocument> builder)
    {
        builder.ToTable("generated_documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(d => d.ApplicationId)
            .HasColumnName("application_id")
            .IsRequired();

        builder.Property(d => d.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.PdfBlobUrl)
            .HasColumnName("pdf_blob_url")
            .IsRequired();

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
