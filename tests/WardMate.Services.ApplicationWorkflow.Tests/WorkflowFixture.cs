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
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class WorkflowFixture : IAsyncLifetime
{
    private readonly string key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine")
        .WithDatabase("workflow_test").WithUsername("workflow_test").WithPassword(Guid.NewGuid().ToString("N")).Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public FakeDirectory Directory { get; } = new();
    public static readonly Guid ProcedureId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid EmptyProcedureId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = key, ["Jwt:Issuer"] = "wardmate", ["Jwt:Audience"] = "wardmate-client", ["Database:AutoMigrate"] = "true"
            }));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<DbContextOptions<WorkflowDbContext>>();
                s.RemoveAll<WorkflowDbContext>();
                s.AddDbContext<WorkflowDbContext>(o => o.UseNpgsql(postgres.GetConnectionString()).UseSnakeCaseNamingConvention());
                s.RemoveAll<IProcedureCatalogClient>();
                s.AddSingleton<IProcedureCatalogClient, FakeCatalog>();
                s.RemoveAll<IWorkflowDirectory>(); s.AddSingleton<IWorkflowDirectory>(Directory);
            });
        });
        using var client = Factory.CreateClient();
        (await client.GetAsync("/health")).EnsureSuccessStatusCode();
    }
    public HttpClient Client(Guid userId, bool admin = false, bool officer = false, string? wardCode = "WARD_A", string[]? permissions = null)
    {
        var role = officer ? "FRONT_DESK_OFFICER" : admin ? "IT_ADMIN" : "REGISTERED_CITIZEN";
        var rights = permissions ?? WardMate.SharedKernel.Web.FeaturePermissions.Workflow.Where(p => officer ||
            p is "workflow.create" or "workflow.read" or "workflow.checklist.write" or "workflow.submit" or "workflow.resubmit" or "workflow.comments.read" or "workflow.versions.read" or "workflow.diff.read").ToArray();
        Directory.Users[userId] = new(userId, wardCode, [role], rights);
        var token = new JwtSecurityToken("wardmate", "wardmate-client",
            [new Claim("sub", userId.ToString()), new Claim("role", officer ? "FRONT_DESK_OFFICER" : admin ? "IT_ADMIN" : "REGISTERED_CITIZEN")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    public async Task DisposeAsync() { await Factory.DisposeAsync(); await postgres.DisposeAsync(); }

    public sealed class FakeDirectory : IWorkflowDirectory
    {
        public System.Collections.Concurrent.ConcurrentDictionary<Guid, WorkflowAccess> Users { get; } = new();
        public bool Unavailable { get; set; }
        public Task<WorkflowAccess?> Access(string token, CancellationToken ct)
        {
            if (Unavailable) throw new HttpRequestException("IAM unavailable");
            var id = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Subject);
            return Task.FromResult(Users.TryGetValue(id, out var value) ? value : null);
        }
        public Task<bool> WardExists(string code, CancellationToken ct) => Task.FromResult(code is "WARD_A" or "WARD_B");
    }
    private sealed class FakeCatalog : IProcedureCatalogClient
    {
        public Task<WorkflowResult<ProcedureSnapshot>> Get(Guid id, CancellationToken ct) => Task.FromResult(
            id == ProcedureId ? WorkflowResult<ProcedureSnapshot>.Ok(new(id, "Thủ tục kiểm thử", true,
                new([new("A"), new("B")]),
                [new("COMMON", "Giấy tờ chung", true, null), new("CASE_A", "Giấy tờ A", true, "A"),
                 new("OPTIONAL", "Giấy tờ tùy chọn", false, "A"), new("CASE_B", "Giấy tờ B", true, "B")]))
            : id == EmptyProcedureId ? WorkflowResult<ProcedureSnapshot>.Ok(new(id, "Thủ tục không có checklist", true, new([]), null))
            : WorkflowResult<ProcedureSnapshot>.Fail(404, "application.procedure_not_found", "Không tìm thấy thủ tục."));
    }
}


