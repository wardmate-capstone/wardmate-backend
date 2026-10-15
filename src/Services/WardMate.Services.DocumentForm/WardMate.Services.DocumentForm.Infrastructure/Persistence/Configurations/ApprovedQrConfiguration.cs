using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class ApprovedQrConfiguration : IEntityTypeConfiguration<ApprovedQr>
{
    public void Configure(EntityTypeBuilder<ApprovedQr> builder)
    {
        builder.ToTable("approved_qrs");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()")
            .ValueGeneratedOnAdd();

        builder.Property(q => q.ApplicationId)
            .HasColumnName("application_id")
            .IsRequired();

        builder.Property(q => q.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        // Mỗi hồ sơ hành chính chỉ có đúng 1 mã QR
        builder.HasIndex(q => q.ApplicationId)
            .IsUnique()
            .HasDatabaseName("ix_approved_qrs_application_id");

        builder.HasIndex(q => q.SubmissionId)
            .HasDatabaseName("ix_approved_qrs_submission_id");

        builder.Property(q => q.QrCodeValue)
            .HasColumnName("qr_code_value")
            .HasMaxLength(2048)
            .IsRequired();

        // Lưu ảnh PNG dưới dạng bytea
        builder.Property(q => q.QrImagePng)
            .HasColumnName("qr_image_png")
            .IsRequired();

        builder.Property(q => q.GeneratedAt)
            .HasColumnName("generated_at")
            .HasColumnType("timestamptz");
    }
}
