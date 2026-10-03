using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Configurations;

public sealed class ProcedureDraftConfiguration : IEntityTypeConfiguration<ProcedureDraft>
{
    public void Configure(EntityTypeBuilder<ProcedureDraft> b)
    {
        b.ToTable("procedure_drafts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.BlobName).HasMaxLength(255);
        b.Property(x => x.PdfFileName).HasMaxLength(255);
        b.Property(x => x.OriginalPdfUrl).HasMaxLength(500);
        b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        b.Property(x => x.WarningsJson).HasColumnType("jsonb");
        b.Property(x => x.CreatedBy).HasMaxLength(255);
        b.Property(x => x.ReviewedBy).HasMaxLength(255);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => new { x.Status, x.LeaseUntil });
    }
}
