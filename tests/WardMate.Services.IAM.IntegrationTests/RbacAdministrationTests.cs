using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Rbac;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.Services.IAM.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.IAM.IntegrationTests;

public sealed class RbacAdministrationTests(IamFixture fixture) : IClassFixture<IamFixture>, IAsyncLifetime
{
    private const string Root = "/api/v1/rbac";
    private const string Password = "ValidPassword123!";
    public async Task InitializeAsync()
    {
        // This class owns its own fixture/database. Each test starts with no administrators.
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        await db.Users.ExecuteDeleteAsync();
        await db.Roles.Where(x => x.Id > 5).ExecuteDeleteAsync();
        await db.RbacAuditLogs.ExecuteDeleteAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient Client, Guid Id, AuthResponseDto Tokens)> Account(bool admin = false)
    {
        var client = fixture.Factory.CreateClient();
        var username = "rbac-" + Guid.NewGuid().ToString("N");
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email = username + "@example.test", password = Password, fullName = "Người dùng kiểm thử" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = (await register.Content.ReadFromJsonAsync<CurrentUserDto>())!;
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
        return (client, user.Id, tokens);
    }
    private static async Task<RoleDto> Create(HttpClient client, string name = "CUSTOM_OFFICER")
    {
        using var response = await client.PostAsJsonAsync(Root + "/roles", new RoleInput(name, "Vai trò thử nghiệm"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<RoleDto>())!;
    }
    private static async Task ExpectCode(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(code, json.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task RoleCrudNormalizesNamesProtectsUniquenessAndRecordsAudit()
    {
        var actor = await Account(true);
        using var client = actor.Client;
        var created = await Create(client, "custom_officer");
        Assert.True(created.Id > 5);
        Assert.False(created.IsSystem);
        Assert.Equal("CUSTOM_OFFICER", created.RoleName);
        Assert.Empty(created.Permissions);
        var role = await client.GetFromJsonAsync<RoleDto>($"{Root}/roles/{created.Id}");
        Assert.Equal(created.Id, role!.Id);
        await ExpectCode(await client.PostAsJsonAsync(Root + "/roles", new RoleInput("Custom_Officer", null)), HttpStatusCode.Conflict, "iam.rbac_conflict");
        using var update = await client.PutAsJsonAsync($"{Root}/roles/{created.Id}", new RoleInput("RENAMED_ROLE", "Mô tả mới"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("RENAMED_ROLE", (await update.Content.ReadFromJsonAsync<RoleDto>())!.RoleName);
        var list = await client.GetFromJsonAsync<RbacPage<RoleDto>>(Root + "/roles?page=1&pageSize=100");
        Assert.Equal(6, list!.Total);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Root}/roles/{created.Id}")).StatusCode);
        await ExpectCode(await client.GetAsync($"{Root}/roles/{created.Id}"), HttpStatusCode.NotFound, "iam.role_not_found");
        var audit = await client.GetFromJsonAsync<RbacPage<AuditDto>>(Root + "/audit-logs");
        Assert.Equal(3, audit!.Total);
        Assert.All(audit.Items, x => { Assert.Equal(actor.Id, x.ActorUserId); Assert.Equal(created.Id, x.RoleId); });
        Assert.Contains(audit.Items, x => x.Action == "role.deleted" && x.Details.Contains("RENAMED_ROLE"));
    }

    [Fact]
    public async Task AssignmentsAreIdempotentAndPermissionChangesAffectExistingJwt()
    {
        var admin = await Account(true);
        var citizen = await Account();
        using var client = admin.Client;
        using var user = citizen.Client;
        var role = await Create(client);
        var permissionRoute = $"{Root}/roles/{role.Id}/permissions/2";
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(permissionRoute, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(permissionRoute, null)).StatusCode);
        var route = $"{Root}/users/{citizen.Id}/roles/{role.Id}";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(route, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(route, null)).StatusCode);
        var roles = await client.GetFromJsonAsync<RoleDto[]>($"{Root}/users/{citizen.Id}/roles");
        Assert.Contains(roles!, x => x.Id == role.Id && x.Permissions.Any(p => p.Id == 2));
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/accounts")).StatusCode);
        // Having iam.manage alone never grants RBAC administration.
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(Root + "/roles")).StatusCode);
        await ExpectCode(await client.DeleteAsync($"{Root}/roles/{role.Id}"), HttpStatusCode.Conflict, "iam.role_in_use");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(permissionRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(permissionRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/accounts")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PermissionDto[]>($"{Root}/roles/{role.Id}/permissions"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route)).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        Assert.Equal(1, await db.RbacAuditLogs.CountAsync(x => x.RoleId == role.Id && x.Action == "role.assigned"));
        Assert.Equal(1, await db.RbacAuditLogs.CountAsync(x => x.RoleId == role.Id && x.Action == "permission.granted"));
    }

    [Fact]
    public async Task EveryRbacOperationRequiresAnActiveAdministrator()
    {
        var citizen = await Account();
        using var client = citizen.Client;
        var operations = new (HttpMethod Method, string Route)[]
        {
            (HttpMethod.Get, "/roles"), (HttpMethod.Post, "/roles"), (HttpMethod.Get, "/roles/1"),
            (HttpMethod.Put, "/roles/1"), (HttpMethod.Delete, "/roles/1"), (HttpMethod.Get, "/permissions"),
            (HttpMethod.Get, "/roles/1/permissions"), (HttpMethod.Put, "/roles/1/permissions/1"), (HttpMethod.Delete, "/roles/1/permissions/1"),
            (HttpMethod.Get, $"/users/{citizen.Id}/roles"), (HttpMethod.Put, $"/users/{citizen.Id}/roles/5"),
            (HttpMethod.Delete, $"/users/{citizen.Id}/roles/1"), (HttpMethod.Get, "/audit-logs")
        };
        foreach (var authenticated in new[] { true, false })
        {
            if (!authenticated) client.DefaultRequestHeaders.Authorization = null;
            foreach (var operation in operations)
            {
                using var request = new HttpRequestMessage(operation.Method, Root + operation.Route)
                { Content = JsonContent.Create(new RoleInput("ANY_ROLE", null)) };
                using var response = await client.SendAsync(request);
                Assert.Equal(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task SystemRolesAndAdminPermissionCannotBeDestroyed()
    {
        var admin = await Account(true);
        using var client = admin.Client;
        for (var id = 1; id <= 5; id++)
        {
            await ExpectCode(await client.DeleteAsync($"{Root}/roles/{id}"), HttpStatusCode.Conflict, "iam.system_role_protected");
            await ExpectCode(await client.PutAsJsonAsync($"{Root}/roles/{id}", new RoleInput("NEW_NAME", null)), HttpStatusCode.Conflict, "iam.system_role_protected");
        }
        await ExpectCode(await client.DeleteAsync(Root + "/roles/5/permissions/2"), HttpStatusCode.Conflict, "iam.admin_permission_protected");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + "/roles/5", new RoleInput("IT_ADMIN", "Quản trị hệ thống"))).StatusCode);
    }

    [Fact]
    public async Task LastActiveAdminCannotBeRevokedOrDisabledEvenWithInactiveAdminPresent()
    {
        var admin = await Account(true);
        var inactive = await Account(true);
        var manager = await Account();
        using var client = admin.Client;
        using var inactiveClient = inactive.Client;
        using var managerClient = manager.Client;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/accounts/{inactive.Id}/status", new { isActive = false })).StatusCode);
        await ExpectCode(await client.DeleteAsync($"{Root}/users/{admin.Id}/roles/5"), HttpStatusCode.Conflict, "iam.last_admin");
        var role = await Create(client, "ACCOUNT_MANAGER");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"{Root}/roles/{role.Id}/permissions/2", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"{Root}/users/{manager.Id}/roles/{role.Id}", null)).StatusCode);
        await ExpectCode(await managerClient.PutAsJsonAsync($"/api/v1/accounts/{admin.Id}/status", new { isActive = false }), HttpStatusCode.Conflict, "iam.last_admin");
        Assert.Equal(HttpStatusCode.Unauthorized, (await inactiveClient.GetAsync(Root + "/roles")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentSelfRevocationsLeaveExactlyOneAdministrator()
    {
        var first = await Account(true);
        var second = await Account(true);
        using var one = first.Client;
        using var two = second.Client;
        var responses = await Task.WhenAll(one.DeleteAsync($"{Root}/users/{first.Id}/roles/5"), two.DeleteAsync($"{Root}/users/{second.Id}/roles/5"));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        Assert.Equal(1, await db.UserRoles.CountAsync(x => x.RoleId == 5 && x.User.IsActive));
        Assert.Equal(1, await db.RbacAuditLogs.CountAsync(x => x.Action == "role.revoked"));
    }

    [Fact]
    public async Task ConcurrentCrossDisablesRecheckActorAndKeepOneAdmin()
    {
        var first = await Account(true);
        var second = await Account(true);
        using var one = first.Client;
        using var two = second.Client;
        var responses = await Task.WhenAll(one.PutAsJsonAsync($"/api/v1/accounts/{second.Id}/status", new { isActive = false }),
            two.PutAsJsonAsync($"/api/v1/accounts/{first.Id}/status", new { isActive = false }));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, x => x.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IamDbContext>().Users.CountAsync(x => x.IsActive));
    }

    [Fact]
    public async Task GrantAndRevokeAdminTakeEffectWithoutNewLoginAndRefreshReflectsLatestRoles()
    {
        var admin = await Account(true);
        var citizen = await Account();
        using var client = admin.Client;
        using var user = citizen.Client;
        var route = $"{Root}/users/{citizen.Id}/roles/5";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(route, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(Root + "/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(Root + "/roles")).StatusCode);
        using var refreshed = await user.PostAsJsonAsync("/api/v1/auth/refresh-token", new { citizen.Tokens.AccessToken, citizen.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var tokens = (await refreshed.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.DoesNotContain(jwt.Claims, x => x.Type == "role" && x.Value == "IT_ADMIN");
    }

    [Fact]
    public async Task MissingTargetsValidationAndDuplicateRenameDoNotWriteAudit()
    {
        var admin = await Account(true);
        using var client = admin.Client;
        var first = await Create(client, "ROLE_ONE");
        var second = await Create(client, "ROLE_TWO");
        await ExpectCode(await client.PutAsJsonAsync($"{Root}/roles/{second.Id}", new RoleInput("role_one", null)), HttpStatusCode.Conflict, "iam.rbac_conflict");
        await ExpectCode(await client.PutAsync($"{Root}/roles/{first.Id}/permissions/99999", null), HttpStatusCode.NotFound, "iam.permission_not_found");
        await ExpectCode(await client.PutAsync($"{Root}/users/{Guid.NewGuid()}/roles/{first.Id}", null), HttpStatusCode.NotFound, "iam.user_not_found");
        await ExpectCode(await client.PutAsync($"{Root}/users/{admin.Id}/roles/99999", null), HttpStatusCode.NotFound, "iam.role_not_found");
        await ExpectCode(await client.GetAsync(Root + "/roles?pageSize=101"), HttpStatusCode.BadRequest, "validation_failed");
        await ExpectCode(await client.GetAsync(Root + "/audit-logs?page=0"), HttpStatusCode.BadRequest, "validation_failed");
        using var invalid = await client.PostAsJsonAsync(Root + "/roles", new RoleInput("", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Tên vai trò không được để trống.", problem.ToString());
        var audit = await client.GetFromJsonAsync<RbacPage<AuditDto>>(Root + "/audit-logs");
        Assert.Equal(2, audit!.Total);
        Assert.Equal("ROLE_TWO", (await client.GetFromJsonAsync<RoleDto>($"{Root}/roles/{second.Id}"))!.RoleName);
    }

    [Fact]
    public async Task SwaggerExposesRbacGroupWithBearerAndTypedResponses()
    {
        using var client = fixture.Factory.CreateClient();
        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var operations = swagger.GetProperty("paths").EnumerateObject().Where(x => x.Name.StartsWith(Root + "/", StringComparison.Ordinal))
            .SelectMany(x => x.Value.EnumerateObject()).ToArray();
        Assert.Equal(13, operations.Length);
        Assert.All(operations, x => { Assert.True(x.Value.TryGetProperty("security", out _)); Assert.Contains("Rbac", x.Value.GetProperty("tags").EnumerateArray().Select(t => t.GetString())); });
        var response = swagger.GetProperty("paths").GetProperty(Root + "/roles").GetProperty("post").GetProperty("responses");
        Assert.True(response.GetProperty("201").TryGetProperty("content", out _));
    }
}
