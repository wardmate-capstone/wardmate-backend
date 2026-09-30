using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public sealed class ProcedureDbContextFactory : IDesignTimeDbContextFactory<ProcedureDbContext>
{
    public ProcedureDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ProcedureDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__ProcedureDatabase")
            ?? "Host=localhost;Port=5434;Database=wardmate_procedure_db;Username=wardmate_procedure")
        .UseSnakeCaseNamingConvention().Options);
}
