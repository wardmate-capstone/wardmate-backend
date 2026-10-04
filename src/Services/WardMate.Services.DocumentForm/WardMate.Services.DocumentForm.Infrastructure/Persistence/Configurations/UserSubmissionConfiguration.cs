using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Domain.Models;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class UserSubmissionConfiguration : IEntityTypeConfiguration<UserSubmission>
{
    public void Configure(EntityTypeBuilder<UserSubmission> builder)
    {
        builder.ToTable("user_submissions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(s => s.TemplateId)
            .HasColumnName("template_id")
            .IsRequired();

        builder.Property(s => s.ApplicantId)
            .HasColumnName("applicant_id")
            .IsRequired();

        builder.Property(s => s.BlobUrl)
            .HasColumnName("blob_url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(s => s.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(s => s.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()       // Lưu dưới dạng text: "Draft", "Submitted", ...
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.OfficerComment)
            .HasColumnName("officer_comment")
            .HasMaxLength(2000);

        builder.Property(s => s.ReviewedByOfficerId)
            .HasColumnName("reviewed_by_officer_id");

        builder.Property(s => s.SubmittedAt)
            .HasColumnName("submitted_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(s => s.DeletedAtUtc)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.Property(s => s.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);

        // Index để truy vấn nhanh hồ sơ theo người dùng và trạng thái
        builder.HasIndex(s => s.ApplicantId)
            .HasDatabaseName("ix_user_submissions_applicant_id");

        builder.HasIndex(s => new { s.ApplicantId, s.Status })
            .HasDatabaseName("ix_user_submissions_applicant_status");
    }
}
