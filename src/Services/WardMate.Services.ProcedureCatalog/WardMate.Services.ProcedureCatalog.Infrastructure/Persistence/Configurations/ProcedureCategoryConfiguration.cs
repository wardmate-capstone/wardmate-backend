using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Configurations;

public sealed class ProcedureCategoryConfiguration : IEntityTypeConfiguration<ProcedureCategory>
{
    public void Configure(EntityTypeBuilder<ProcedureCategory> builder)
    {
        builder.ToTable("procedure_categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();
        builder.Property(x => x.CategoryName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.HasMany(x => x.Procedures).WithOne(x => x.Category).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
