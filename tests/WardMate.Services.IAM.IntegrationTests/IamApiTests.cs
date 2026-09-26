using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Persistence;
using WardMate.Services.IAM.Infrastructure.Security;
using Xunit;

namespace WardMate.Services.IAM.IntegrationTests;

public sealed class IamApiTests(IamFixture fixture) : IClassFixture<IamFixture>
{
    private const string Password = "IntegrationPassword123!";
    private async Task<(CurrentUserDto User, AuthResponseDto Tokens)> RegisterAndLogin(HttpClient client)
    {
        var username = "citizen-" + Guid.NewGuid().ToString("N");
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email = username + "@example.test", password = Password, fullName = "Nguyen Van A" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registrationJson = await register.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Nguyen Van A", registrationJson.GetProperty("profile").GetProperty("fullName").GetString());
        Assert.Single(registrationJson.GetProperty("profile").EnumerateObject());
        var user = registrationJson.Deserialize<CurrentUserDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { usernameOrEmail = username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (user, (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!);
    }
    [Fact]
    public async Task StartupMigratesSevenTablesAndSeedsFiveRoles()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        Assert.Equal(5, await db.Roles.CountAsync());
        Assert.Equal(3, await db.Permissions.CountAsync());
        Assert.Equal(11, await db.RolePermissions.CountAsync());
        Assert.Equal(7, db.Model.GetEntityTypes().Count());
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
        await db.Database.MigrateAsync();
        Assert.Equal(5, await db.Roles.CountAsync());
    }
    [Fact]
    public async Task CurrentProfileOmitsNullFieldsAndPreservesPopulatedFields()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Tokens.AccessToken);
        var initial = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me");
        Assert.Single(initial.GetProperty("profile").EnumerateObject());
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
            var profile = await db.Set<UserProfile>().SingleAsync(x => x.UserId == account.User.Id);
            profile.PhoneNumber = "0901234567";
            profile.DateOfBirth = new DateOnly(2000, 1, 2);
            profile.TemporaryAddress = "";
            await db.SaveChangesAsync();
        }
        var updated = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me");
        var json = updated.GetProperty("profile");
        Assert.Equal(4, json.EnumerateObject().Count());
        Assert.Equal("Nguyen Van A", json.GetProperty("fullName").GetString());
        Assert.Equal("0901234567", json.GetProperty("phoneNumber").GetString());
        Assert.Equal("2000-01-02", json.GetProperty("dateOfBirth").GetString());
        Assert.Equal("", json.GetProperty("temporaryAddress").GetString());
        Assert.False(json.TryGetProperty("identityNumber", out _));
    }
    [Theory]
    [InlineData("Abcdef!", HttpStatusCode.BadRequest)]
    [InlineData("abcdefg!", HttpStatusCode.BadRequest)]
    [InlineData("Abcdefgh", HttpStatusCode.BadRequest)]
    [InlineData("Abcdefg!", HttpStatusCode.Created)]
    public async Task RegistrationPasswordPolicyIsExposedThroughApi(string password, HttpStatusCode expected)
    {
        using var client = fixture.Factory.CreateClient();
        var username = "policy-" + Guid.NewGuid().ToString("N");
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email = username + "@example.test", password, fullName = "Test citizen" });
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
            Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
            using var scope = fixture.Factory.Services.CreateScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<IamDbContext>().Users.AnyAsync(x => x.Username == username));
        }
    }
    [Fact]
    public async Task CompleteAuthLifecycleAndOwnershipChecks()
    {
        using var client = fixture.Factory.CreateClient();
        var first = await RegisterAndLogin(client);
        var second = await RegisterAndLogin(client);
        Assert.Equal("REGISTERED_CITIZEN", Assert.Single(first.User.Roles));
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IamDbContext>().RefreshTokens.SingleAsync(x => x.UserId == first.User.Id);
            Assert.NotEqual(first.Tokens.RefreshToken, stored.Token);
            Assert.Equal(64, stored.Token.Length);
        }
        using var unauthorized = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("application/problem+json", unauthorized.Content.Headers.ContentType?.MediaType);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.Tokens.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/v1/users/me");
        Assert.Equal(first.User.Id, me!.Id);
        Assert.Contains("iam.profile.read", me.Permissions);
        using var wrongOwner = await client.PostAsJsonAsync("/api/v1/auth/revoke-token", new { second.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongOwner.StatusCode);
        using var mismatched = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { first.Tokens.AccessToken, second.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, mismatched.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { first.Tokens.AccessToken, first.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = (await refresh.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        using var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { first.Tokens.AccessToken, first.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated.AccessToken);
        using var revoke = await client.PostAsJsonAsync("/api/v1/auth/revoke-token", new { rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var revoked = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { rotated.AccessToken, rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }
    [Fact]
    public async Task ValidationAndDuplicateRegistrationReturnProblemDetails()
    {
        using var client = fixture.Factory.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/register", new { username = "", email = "bad", password = "short", fullName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal("Dữ liệu không hợp lệ.", problem.GetProperty("title").GetString());
        Assert.Contains("Mật khẩu phải có ít nhất 8 ký tự.", problem.GetProperty("errors").GetProperty("password").EnumerateArray().Select(x => x.GetString()));
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));
        var account = await RegisterAndLogin(client);
        using var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new { username = account.User.Username.ToUpperInvariant(), email = "new@example.test", password = Password, fullName = "Test" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { usernameOrEmail = account.User.Username, password = "not-correct" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }
    [Fact]
    public async Task PostgreSqlRefreshConcurrencyRollsBackLosingReplacement()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await RegisterAndLogin(client);
        using var one = fixture.Factory.Services.CreateScope();
        using var two = fixture.Factory.Services.CreateScope();
        var first = one.ServiceProvider.GetRequiredService<IIdentityStore>();
        var second = two.ServiceProvider.GetRequiredService<IIdentityStore>();
        var tokens = one.ServiceProvider.GetRequiredService<ITokenService>();
        var hash = tokens.HashRefreshToken(account.Tokens.RefreshToken);
        var oldOne = (await first.FindRefreshToken(hash, default))!;
        var oldTwo = (await second.FindRefreshToken(hash, default))!;
        oldOne.IsRevoked = oldTwo.IsRevoked = true;
        var winner = new RefreshToken { UserId = account.User.Id, Token = tokens.CreateRefreshToken().Hash, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var loser = new RefreshToken { UserId = account.User.Id, Token = tokens.CreateRefreshToken().Hash, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        first.AddRefreshToken(winner);
        second.AddRefreshToken(loser);
        Assert.Equal(SaveOutcome.Saved, await first.SaveChanges(default));
        Assert.Equal(SaveOutcome.ConcurrentUpdate, await second.SaveChanges(default));
        var db = two.ServiceProvider.GetRequiredService<IamDbContext>();
        Assert.False(await db.RefreshTokens.AnyAsync(x => x.Id == loser.Id));
        Assert.Equal(1, await db.RefreshTokens.CountAsync(x => x.UserId == account.User.Id && !x.IsRevoked));
    }
    [Fact]
    public async Task ExpiredAccessCanRefreshButCannotAuthorizeAndExpiredRefreshIsRejected()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await RegisterAndLogin(client);
        var settings = fixture.Factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var expiredJwt = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new("sub", account.User.Id.ToString())], DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        var expired = new JwtSecurityTokenHandler().WriteToken(expiredJwt);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me")).StatusCode);
        using var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { accessToken = expired, account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<ITokenService>().HashRefreshToken(rotated.RefreshToken);
        var record = await db.RefreshTokens.SingleAsync(x => x.Token == hash);
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { rotated.AccessToken, rotated.RefreshToken })).StatusCode);
    }
    [Fact]
    public async Task ConcurrentRegistrationCreatesExactlyOneAccount()
    {
        using var client = fixture.Factory.CreateClient();
        var username = "race-" + Guid.NewGuid().ToString("N");
        var body = new { username, email = username + "@example.test", password = Password, fullName = "Concurrent user" };
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/auth/register", body), client.PostAsJsonAsync("/api/v1/auth/register", body));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            using var scope = fixture.Factory.Services.CreateScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IamDbContext>().Users.CountAsync(x => x.Username == username));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    [Fact]
    public async Task DisabledAccountCannotRefreshOrReadCurrentProfile()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await RegisterAndLogin(client);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == account.User.Id);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { account.Tokens.AccessToken, account.Tokens.RefreshToken })).StatusCode);
    }
    [Fact]
    public async Task SwaggerExposesFiveRequestedEndpointsAndBearerSecurity()
    {
        using var client = fixture.Factory.CreateClient();
        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        foreach (var route in new[] { "/api/v1/auth/register", "/api/v1/auth/login", "/api/v1/auth/refresh-token", "/api/v1/auth/revoke-token", "/api/v1/users/me" })
            Assert.True(swagger.GetProperty("paths").TryGetProperty(route, out _));
        Assert.Equal("bearer", swagger.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(swagger.GetProperty("paths").GetProperty("/api/v1/users/me").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(swagger.GetProperty("paths").GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"username\":123}")]
    [InlineData("{broken")]
    public async Task InvalidRequestBodiesReturnVietnameseMessages(string body)
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsync("/api/v1/auth/register", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Dữ liệu không hợp lệ.", problem.GetProperty("title").GetString());
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.All(problem.GetProperty("errors").EnumerateObject(), field =>
            Assert.All(field.Value.EnumerateArray(), message =>
                Assert.Equal("Trường dữ liệu bị thiếu hoặc không đúng định dạng.", message.GetString())));
    }
    [Fact]
    public async Task AuthenticationFailuresReturnVietnameseMessages()
    {
        using var client = fixture.Factory.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/users/me");
        var unauthorized = await anonymous.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Vui lòng đăng nhập để tiếp tục.", unauthorized.GetProperty("title").GetString());
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { usernameOrEmail = "missing", password = "WrongPassword!" });
        var problem = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("iam.invalid_credentials", problem.GetProperty("code").GetString());
        Assert.Equal("Thông tin đăng nhập không đúng hoặc tài khoản chưa được kích hoạt.", problem.GetProperty("title").GetString());
    }
}
