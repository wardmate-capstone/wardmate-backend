using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using Xunit;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class ProcedureFixture : IAsyncLifetime
{
    private readonly string signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
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
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = signingKey, ["Jwt:Issuer"] = "wardmate", ["Jwt:Audience"] = "wardmate-client",
                ["Database:AutoMigrate"] = "true",
                ["AzureBlob:ConnectionString"] = "test-storage-only",
                ["ProcedureDrafts:ExtractionEnabled"] = "false"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<WardMate.SharedKernel.Blob.IBlobStorageClient>();
                services.AddSingleton<WardMate.SharedKernel.Blob.IBlobStorageClient, DraftTestBlobStorage>();
                services.RemoveAll<DbContextOptions<ProcedureDbContext>>();
                services.RemoveAll<ProcedureDbContext>();
                services.AddDbContext<ProcedureDbContext>(options => options.UseNpgsql(postgres.GetConnectionString()).UseSnakeCaseNamingConvention());
            });
        });
        using var client = Factory.CreateClient();
        (await client.GetAsync("/health")).EnsureSuccessStatusCode();
    }
    public HttpClient ManagerClient(string role = "PROCEDURE_MANAGER", bool expired = false, bool invalidSignature = false)
    {
        var key = invalidSignature ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)) : signingKey;
        var token = new JwtSecurityToken("wardmate", "wardmate-client",
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)],
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(expired ? -1 : 10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
