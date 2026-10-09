using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.IAM.Tests;

public sealed class IamFixture : IAsyncLifetime
{
    private readonly string key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine")
        .WithDatabase("iam_test").WithUsername("iam_test").WithPassword(Guid.NewGuid().ToString("N")).Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Jwt:Key"] = key, ["Database:AutoMigrate"] = "true" }));
            b.ConfigureServices(s =>
            {
                s.RemoveAll<DbContextOptions<IamDbContext>>(); s.RemoveAll<IamDbContext>();
                s.AddDbContext<IamDbContext>(o => o.UseNpgsql(postgres.GetConnectionString()).UseSnakeCaseNamingConvention());
            });
        });
        using var client = Factory.CreateClient(); (await client.GetAsync("/health")).EnsureSuccessStatusCode();
    }
    public async Task<Guid> Ward(string code)
    {
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var ward = new Ward { Code = code.ToUpperInvariant(), Name = "Phường test " + code }; db.Wards.Add(ward); await db.SaveChangesAsync(); return ward.Id;
    }
    public async Task<Guid> User(int role, Guid? ward = null, int[]? categories = null, string? name = null, string? identity = null)
    {
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var id = Guid.NewGuid(); var u = new User { Id = id, Username = "user" + id.ToString("N"), Email = id.ToString("N") + "@example.invalid",
            PasswordHash = "test-only-not-a-password", WardId = ward, AssignedCategories = categories ?? [], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Profile = new UserProfile { UserId = id, FullName = name ?? "Người kiểm thử", IdentityNumber = identity, UpdatedAt = DateTime.UtcNow },
            UserRoles = [new UserRole { UserId = id, RoleId = role }] };
        db.Users.Add(u); await db.SaveChangesAsync(); return id;
    }
    public HttpClient Client(Guid id)
    {
        // Claims intentionally carry no permissions: IAM must evaluate live database grants.
        var jwt = new JwtSecurityToken("wardmate", "wardmate-client", [new Claim("sub", id.ToString())],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        var client = Factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        return client;
    }
    public async Task DisposeAsync() { await Factory.DisposeAsync(); await postgres.DisposeAsync(); }
}

