using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class ProcedureFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine")
        .WithDatabase("wardmate_procedure_test_db").WithUsername("procedure_test")
        .WithPassword(Guid.NewGuid().ToString("N")).Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ProcedureDbContext>>();
                services.RemoveAll<ProcedureDbContext>();
                services.AddDbContext<ProcedureDbContext>(options => options.UseNpgsql(postgres.GetConnectionString()).UseSnakeCaseNamingConvention());
            });
        });
        using var client = Factory.CreateClient();
        (await client.GetAsync("/health")).EnsureSuccessStatusCode();
    }
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
