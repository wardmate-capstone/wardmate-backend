using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.IAM.IntegrationTests;

public sealed class ProfileRbacTests(IamFixture fixture) : IClassFixture<IamFixture>
{
    private const string Password = "ValidPassword123!";
    private static object Profile(string? identityNumber = null) => new { fullName = "Nguyễn Văn An", identityNumber, phoneNumber = "0901234567", dateOfBirth = "2000-01-02", gender = "Nam" };

    private async Task<(HttpClient Client, Guid Id, AuthResponseDto Tokens, string Username)> Account(bool admin = false)
    {
        var client = fixture.Factory.CreateClient();
        var username = "rbac-" + Guid.NewGuid().ToString("N");
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email = username + "@example.test", password = Password, fullName = "Test" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var user = (await registered.Content.ReadFromJsonAsync<CurrentUserDto>())!;
        if (admin)
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = 5 });
            await db.SaveChangesAsync();
        }
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { usernameOrEmail = username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, user.Id, tokens, username);
    }

    [Fact]
    public async Task CitizenCanDeleteRecreateReadAndReplaceOwnProfile()
    {
        var account = await Account();
        using var client = account.Client;
        const string route = "/api/v1/users/me/profile";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, Profile())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(route, Profile())).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me");
        Assert.False(me.TryGetProperty("profile", out _));
        using var created = await client.PostAsJsonAsync(route, Profile());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(route, new { fullName = "Tên mới" })).StatusCode);
        var result = await client.GetFromJsonAsync<JsonElement>(route);
        Assert.Equal("Tên mới", result.GetProperty("fullName").GetString());
        Assert.False(result.TryGetProperty("phoneNumber", out _));
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { account.Tokens.AccessToken, account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task CitizenCannotManageOtherProfilesOrAccounts()
    {
        var first = await Account();
        var second = await Account();
        using var client = first.Client;
        using var other = second.Client;
        var route = $"/api/v1/users/{second.Id}/profile";
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, route) { Content = JsonContent.Create(Profile()) };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/accounts/{second.Id}/status", new { isActive = false })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me/profile")).StatusCode);
    }

    [Fact]
    public async Task AdminCanManageProfilesAndDisableThenEnableAccount()
    {
        var admin = await Account(true);
        var citizen = await Account();
        using var client = admin.Client;
        using var target = citizen.Client;
        var route = $"/api/v1/users/{citizen.Id}/profile";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(route, Profile())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(route, Profile())).StatusCode);
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts?page=1&pageSize=1");
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.False(page.GetProperty("items")[0].TryGetProperty("passwordHash", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/accounts?pageSize=101")).StatusCode);
        var status = $"/api/v1/accounts/{citizen.Id}/status";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(status, new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.GetAsync("/api/v1/users/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.PostAsJsonAsync("/api/v1/auth/login", new { usernameOrEmail = citizen.Username, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.PostAsJsonAsync("/api/v1/auth/refresh-token", new { citizen.Tokens.AccessToken, citizen.Tokens.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(status, new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.PostAsJsonAsync("/api/v1/auth/refresh-token", new { citizen.Tokens.AccessToken, citizen.Tokens.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await target.PostAsJsonAsync("/api/v1/auth/login", new { usernameOrEmail = citizen.Username, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/v1/accounts/{admin.Id}/status", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(status, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task RemovingRoleImmediatelyDeniesExistingJwt()
    {
        var admin = await Account(true);
        using var client = admin.Client;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/accounts")).StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
            await db.UserRoles.Where(x => x.UserId == admin.Id && x.RoleId == 5).ExecuteDeleteAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/users/me/profile")).StatusCode);
    }

    [Fact]
    public async Task DuplicateIdentityReturnsConflictAndInvalidProfileReturnsVietnameseValidation()
    {
        var first = await Account();
        var second = await Account();
        using var one = first.Client;
        using var two = second.Client;
        const string route = "/api/v1/users/me/profile";
        Assert.Equal(HttpStatusCode.OK, (await one.PutAsJsonAsync(route, Profile("012345678901"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await two.PutAsJsonAsync(route, Profile("012345678901"))).StatusCode);
        using var invalid = await two.PutAsJsonAsync(route, new { fullName = "", dateOfBirth = "2999-01-01", phoneNumber = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Contains("Họ và tên không được để trống.", problem.ToString());
    }

    [Fact]
    public async Task StaleTokenIssuanceCannotSurviveAccountDisable()
    {
        var account = await Account();
        using var client = account.Client;
        using var pending = fixture.Factory.Services.CreateScope();
        var store = pending.ServiceProvider.GetRequiredService<IIdentityStore>();
        Assert.NotNull(await store.FindUser(account.Id, default));
        store.AddRefreshToken(new RefreshToken { UserId = account.Id, Token = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1) });
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<WardMate.Services.IAM.Application.Accounts.IAccountStore>();
            Assert.True(await accounts.SetActive(account.Id, false, default));
        }
        Assert.Equal(SaveOutcome.ConcurrentUpdate, await store.SaveChanges(default));
    }

    [Fact]
    public async Task SwaggerDocumentsProtectedProfileAndAccountEndpoints()
    {
        using var client = fixture.Factory.CreateClient();
        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = swagger.GetProperty("paths");
        foreach (var route in new[] { "/api/v1/users/me/profile", "/api/v1/users/{userId}/profile" })
        {
            foreach (var method in new[] { "get", "post", "put", "delete" })
                Assert.True(paths.GetProperty(route).GetProperty(method).TryGetProperty("security", out _));
            Assert.True(paths.GetProperty(route).GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
        }
        Assert.True(paths.GetProperty("/api/v1/accounts").GetProperty("get").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/v1/accounts/{userId}/status").GetProperty("put").GetProperty("responses").TryGetProperty("204", out _));
    }
}
