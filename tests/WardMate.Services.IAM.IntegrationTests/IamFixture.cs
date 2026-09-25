using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using WardMate.Services.IAM.Infrastructure.Persistence;
using WardMate.Services.IAM.Infrastructure.Security;
using Xunit;

namespace WardMate.Services.IAM.IntegrationTests;

public sealed class IamFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine")
        .WithDatabase("wardmate_iam_test_db").WithUsername("wardmate").WithPassword(Guid.NewGuid().ToString("N")).Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<IamDbContext>>();
                services.RemoveAll<IamDbContext>();
                services.AddDbContext<IamDbContext>(options => options.UseNpgsql(postgres.GetConnectionString()).UseSnakeCaseNamingConvention());
                services.PostConfigure<JwtOptions>(options => options.Key = "integration-test-signing-key-at-least-32-characters");
            });
        });
        // Start the real API, including its startup migration/seed path.
        using var client = Factory.CreateClient();
        (await client.GetAsync("/health")).EnsureSuccessStatusCode();
    }
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
