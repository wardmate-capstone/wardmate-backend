using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// </summary>
public sealed class DocumentDbContextFactory : IDesignTimeDbContextFactory<DocumentDbContext>
{
    public DocumentDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "WardMate.Services.DocumentForm.API"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DocumentDb")
            ?? "Host=localhost;Port=5432;Database=wardmate_db;Username=wardmate_admin;Password=wardmate_secret";

        var optionsBuilder = new DbContextOptionsBuilder<DocumentDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new DocumentDbContext(optionsBuilder.Options);
    }
}
