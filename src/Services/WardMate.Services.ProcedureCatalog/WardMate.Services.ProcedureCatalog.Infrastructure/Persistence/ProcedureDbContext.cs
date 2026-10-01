using Microsoft.EntityFrameworkCore;
using WardMate.Services.ProcedureCatalog.Domain.Entities;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public sealed class ProcedureDbContext(DbContextOptions<ProcedureDbContext> options) : DbContext(options)
{
    public DbSet<ProcedureCategory> ProcedureCategories => Set<ProcedureCategory>();
    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<ProcedureVersion> ProcedureVersions => Set<ProcedureVersion>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("unaccent");
        builder.HasDbFunction(typeof(ProcedureDbContext).GetMethod(nameof(Unaccent))!).HasName("unaccent");
        builder.ApplyConfigurationsFromAssembly(typeof(ProcedureDbContext).Assembly);
        ProcedureSeed.Configure(builder);
    }
    public static string Unaccent(string value) => throw new NotSupportedException("Chỉ sử dụng hàm này trong truy vấn PostgreSQL.");
}
