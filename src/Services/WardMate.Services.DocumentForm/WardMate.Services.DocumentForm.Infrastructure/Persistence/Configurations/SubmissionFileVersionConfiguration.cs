using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class SubmissionFileVersionConfiguration : IEntityTypeConfiguration<SubmissionFileVersion>
{
    public void Configure(EntityTypeBuilder<SubmissionFileVersion> b)
    {
        b.ToTable("submission_file_versions", t =>
        {
            t.HasCheckConstraint("ck_submission_file_versions_source", "source_type IN ('SUBMISSION','SUPPORTING_DOCUMENT')");
            t.HasCheckConstraint("ck_submission_file_versions_version", "application_version_number >= 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ApplicationId).HasColumnName("application_id");
        b.Property(x => x.ChecklistId).HasColumnName("checklist_id");
        b.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(30).IsRequired();
        b.Property(x => x.SourceId).HasColumnName("source_id");
        b.Property(x => x.ApplicationVersionNumber).HasColumnName("application_version_number");
        b.Property(x => x.BlobUrl).HasColumnName("blob_url").IsRequired();
        b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        b.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
        b.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
        b.Property(x => x.FormData).HasColumnName("form_data").HasColumnType("jsonb");
        b.Property(x => x.SourceUpdatedAtUtc).HasColumnName("source_updated_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.ApplicationId, x.ApplicationVersionNumber, x.SourceType, x.SourceId }).IsUnique()
            .HasDatabaseName("ux_submission_file_versions_source");
    }
}
