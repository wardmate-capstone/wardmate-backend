using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class PermissionTests(ProcedureFixture f) : IClassFixture<ProcedureFixture>
{
    [Theory]
    [InlineData("IT_ADMIN")]
    [InlineData("PROCEDURE_MANAGER")]
    public async Task RoleAloneCannotBypassPermission(string role)
    {
        using var client = f.ManagerClient(role, permissions: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/procedure-manager/procedures")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/procedure-manager/drafts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/procedures")).StatusCode);
    }
    [Fact]
    public async Task ReadOnlyPermissionCannotWrite()
    {
        using var client = f.ManagerClient(permissions: ["procedure.read"]);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/procedure-manager/procedures")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/procedure-manager/procedures", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/procedure-manager/procedures/publish", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/v1/procedure-manager/drafts/{Guid.NewGuid()}")).StatusCode);
    }
}
