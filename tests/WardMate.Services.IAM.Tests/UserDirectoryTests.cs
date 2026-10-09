using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.IAM.Tests;

public sealed class UserDirectoryTests(IamFixture f) : IClassFixture<IamFixture>
{
    [Fact]
    public void DirectoryAndCategoryValidatorsRejectInvalidInput()
    {
        Assert.False(new DirectoryValidator().Validate(new DirectoryQuery(Guid.NewGuid(), false, new(Page: 0, PageSize: 101))).IsValid);
        Assert.False(new DirectoryValidator().Validate(new DirectoryQuery(Guid.NewGuid(), false, new(AssignedCategory: -1))).IsValid);
        Assert.False(new AssignCategoriesValidator().Validate(new AssignCategoriesCommand(Guid.NewGuid(), Guid.NewGuid(), [1, 1])).IsValid);
        Assert.True(new AssignCategoriesValidator().Validate(new AssignCategoriesCommand(Guid.NewGuid(), Guid.NewGuid(), [1, 2])).IsValid);
    }

    [Fact]
    public async Task RevokedReadPermissionBlocksExistingTokenDespiteAdminRole()
    {
        var id = await f.User(5); using var client = f.Client(id);
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var permission = await db.Permissions.SingleAsync(p => p.PermissionCode == "iam.accounts.read");
        await db.RolePermissions.Where(x => x.RoleId == 5 && x.PermissionId == permission.Id).ExecuteDeleteAsync();
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);
        }
        finally { db.RolePermissions.Add(new() { RoleId = 5, PermissionId = permission.Id }); await db.SaveChangesAsync(); }
    }
    [Fact]
    public async Task ManagerSeesOnlyOwnWardAndCategoryBeforePagination()
    {
        var code = "A" + Guid.NewGuid().ToString("N"); var a = await f.Ward(code); var b = await f.Ward("B" + Guid.NewGuid().ToString("N"));
        var manager = await f.User(3, a); var first = await f.User(2, a, [1, 2]); await f.User(2, a, [2]); await f.User(2, b, [1]); await f.User(1, a, [1]);
        using var client = f.Client(manager);
        var all = await client.GetFromJsonAsync<DirectoryPage>("/api/v1/manager/officers?pageSize=1");
        Assert.Equal(2, all!.Total); Assert.Single(all.Items);
        var filtered = await client.GetFromJsonAsync<DirectoryPage>($"/api/v1/manager/officers?wardCode={code}&assignedCategory=1");
        Assert.Equal(first, Assert.Single(filtered!.Items).Id);
        Assert.Empty((await client.GetFromJsonAsync<DirectoryPage>("/api/v1/manager/officers?wardCode=OTHER"))!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
    }
    [Fact]
    public async Task AdminSearchesNameEmailIdentityAndRole()
    {
        var admin = await f.User(5); var marker = Guid.NewGuid().ToString("N");
        var identity = Guid.NewGuid().ToString("N")[..20]; var id = await f.User(2, name: marker, identity: identity);
        using var client = f.Client(admin);
        foreach(var search in new[] { marker, identity, id.ToString("N") + "@example.invalid" })
        {
            var result = await client.GetFromJsonAsync<DirectoryPage>($"/api/v1/admin/users?search={search}&role=FRONT_DESK_OFFICER&pageSize=1");
            Assert.Equal(1, result!.Total); Assert.Equal(id, Assert.Single(result.Items).Id);
        }
        Assert.Empty((await client.GetFromJsonAsync<DirectoryPage>($"/api/v1/admin/users?search={marker}&role=MANAGER"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/admin/users?pageSize=101")).StatusCode);
    }
    [Fact]
    public async Task CategoryAssignmentsRespectWardAndFilterImmediately()
    {
        var a = await f.Ward("A" + Guid.NewGuid().ToString("N")); var b = await f.Ward("B" + Guid.NewGuid().ToString("N"));
        var manager = await f.User(3, a); var own = await f.User(2, a); var other = await f.User(2, b);
        using var client = f.Client(manager);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/manager/officers/{own}/categories", new { categories = new[] { 7, 9 } })).StatusCode);
        Assert.Equal(own, Assert.Single((await client.GetFromJsonAsync<DirectoryPage>("/api/v1/manager/officers?assignedCategory=9"))!.Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/manager/officers/{other}/categories", new { categories = new[] { 7 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/manager/officers/{own}/categories", new { categories = new[] { -1 } })).StatusCode);
    }
    [Fact]
    public async Task CurrentAccessReflectsWardChangesAndDisabledAccounts()
    {
        var a = await f.Ward("A" + Guid.NewGuid().ToString("N")); var bCode = "B" + Guid.NewGuid().ToString("N"); var b = await f.Ward(bCode);
        var id = await f.User(2, a); using var client = f.Client(id);
        var initial = (await client.GetFromJsonAsync<AccessContextDto>("/api/v1/users/access-context"))!;
        Assert.Contains("workflow.approve", initial.Permissions);
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        await db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.WardId, b));
        Assert.Equal(bCode.ToUpperInvariant(), (await client.GetFromJsonAsync<AccessContextDto>("/api/v1/users/access-context"))!.WardCode);
        await db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/access-context")).StatusCode);
    }
    [Fact]
    public async Task RoleWithoutPermissionCannotReadAccounts()
    {
        // Use an isolated custom role instead of mutating grants shared by other tests.
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var id = await f.User(5);
        await db.UserRoles.Where(x => x.UserId == id).ExecuteDeleteAsync();
        using var client = f.Client(id);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
    }
}


