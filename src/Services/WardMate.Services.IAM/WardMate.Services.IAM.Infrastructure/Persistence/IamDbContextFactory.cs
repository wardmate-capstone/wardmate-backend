using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class IamDbContextFactory : IDesignTimeDbContextFactory<IamDbContext>
{
    public IamDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<IamDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Database=wardmate_iam_db;Username=wardmate")
        .UseSnakeCaseNamingConvention().Options);
}
