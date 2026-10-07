using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.DocumentForm.Domain.Entities;
namespace WardMate.Services.DocumentForm.Infrastructure.Persistence.Configurations;

public sealed class FormTemplateVersionConfiguration : IEntityTypeConfiguration<FormTemplateVersion>
{
    public void Configure(EntityTypeBuilder<FormTemplateVersion> b)
    {
        b.ToTable("form_template_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.SchemaDefinition).HasColumnType("jsonb");
        b.Property(x => x.MappingDefinition).HasColumnType("jsonb");
        b.Property(x => x.OriginalBlobUrl).HasMaxLength(2048);
        b.Property(x => x.OriginalSha256).HasMaxLength(64);
        b.HasIndex(x => new { x.TemplateId, x.VersionNumber }).IsUnique();
        b.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
