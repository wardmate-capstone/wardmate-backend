using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class ProcedureListTests(ProcedureFixture fixture) : IClassFixture<ProcedureFixture>
{
    private const string Public = "/api/v1/procedures";
    private const string Manager = "/api/v1/procedure-manager/procedures";

    [Fact]
    public async Task PublicCategoriesSupplyIdsForUnassignedFrontDeskFiltering()
    {
        var prefix = await Seed();
        using var client = fixture.Factory.CreateClient();
        var categories = await client.GetFromJsonAsync<ProcedureCategoryDto[]>($"{Public}/categories");
        Assert.NotNull(categories);
        Assert.Contains(categories, x => x.Id == 1 && !string.IsNullOrWhiteSpace(x.CategoryName));
        Assert.Contains(categories, x => x.Id == 2);
        var first = await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?categoryId=1&keyword={prefix}");
        var second = await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?categoryId=2&keyword={prefix}");
        Assert.Equal(3, first!.TotalCount);
        Assert.Equal(1, second!.TotalCount);
        Assert.All(first.Items, x => Assert.Equal(categories.Single(c => c.Id == 1).CategoryName, x.CategoryName));
    }

    [Fact]
    public async Task AccentInsensitiveKeywordLevelFilterAndDescendingSortReturnCorrectMetadata()
    {
        var prefix = await Seed();
        using var client = fixture.ManagerClient();
        var keyword = Uri.EscapeDataString(prefix + " dang ky thu nghiem");
        var result = (await client.GetFromJsonAsync<PagedResult<ProcedureManagerSummaryDto>>(
            $"{Manager}?keyword={keyword}&levelOfImplementation=cap%20xa&pageNumber=2&pageSize=2&sortBy=ProcedureCode&isAscending=false"))!;
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.CurrentPage);
        Assert.True(result.HasPrevious);
        Assert.True(result.HasNext);
        Assert.Equal(new[] { prefix + "-3", prefix + "-2" }, result.Items.Select(x => x.ProcedureCode));
        var publicResult = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>(
            $"{Public}?keyword={keyword}&isActive=false&pageSize=50"))!;
        Assert.Equal(4, publicResult.TotalCount);
        Assert.DoesNotContain(publicResult.Items, x => x.ProcedureCode == prefix + "-4");
        Assert.False(publicResult.HasPrevious);
        Assert.False(publicResult.HasNext);
    }

    [Theory]
    [InlineData("sortBy=DROP%20TABLE")]
    [InlineData("pageNumber=0")]
    [InlineData("isAscending=invalid")]
    public async Task AdvancedQueryValidationRejectsInvalidParameters(string query)
    {
        using var client = fixture.ManagerClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Public}?{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Manager}?{query}")).StatusCode);
    }

    private async Task<string> Seed()
    {
        var prefix = Guid.NewGuid().ToString("N");
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        for (var i = 1; i <= 5; i++)
        {
            var procedure = ProcedureSeed.CreateSample();
            procedure.Id = Guid.NewGuid();
            procedure.ProcedureCode = $"{prefix}-{i}";
            procedure.Title = $"{prefix} Đăng ký thử nghiệm {i}";
            procedure.CategoryId = i == 5 ? 2 : 1;
            procedure.IsActive = i != 4;
            db.Procedures.Add(procedure);
        }
        await db.SaveChangesAsync();
        return prefix;
    }

    [Fact]
    public async Task PublicPaginatesOnlyActiveRowsWithStableOrderingAndSummaryProjection()
    {
        var prefix = await Seed();
        using var client = fixture.Factory.CreateClient();
        var first = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?search={prefix}&pageSize=2&isActive=false"))!;
        var second = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?search={prefix}&pageSize=2&page=2"))!;
        Assert.Equal(4, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(1, first.CurrentPage);
        Assert.Equal(new[] { $"{prefix}-1", $"{prefix}-2", $"{prefix}-3", $"{prefix}-5" }, first.Items.Concat(second.Items).Select(x => x.ProcedureCode));
        Assert.DoesNotContain(first.Items.Concat(second.Items), x => x.ProcedureCode == prefix + "-4");
        Assert.Equal("Hộ tịch", first.Items[0].CategoryName);
        var raw = await client.GetFromJsonAsync<JsonElement>($"{Public}?search={prefix}");
        Assert.False(raw.GetProperty("items")[0].TryGetProperty("contentPayload", out _));
        var beyond = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?search={prefix}&page=99"))!;
        Assert.Empty(beyond.Items);
        Assert.Equal(4, beyond.TotalCount);
    }

    [Fact]
    public async Task ManagerIncludesInactiveAndCombinesSearchCategoryAndStatusFilters()
    {
        var prefix = await Seed();
        using var client = fixture.ManagerClient();
        var all = (await client.GetFromJsonAsync<PagedResult<ProcedureManagerSummaryDto>>($"{Manager}?search={prefix.ToUpperInvariant()}"))!;
        Assert.Equal(5, all.TotalCount);
        Assert.Equal(10, all.PageSize);
        var inactive = (await client.GetFromJsonAsync<PagedResult<ProcedureManagerSummaryDto>>($"{Manager}?search={prefix}&categoryId=1&isActive=false"))!;
        Assert.Equal($"{prefix}-4", Assert.Single(inactive.Items).ProcedureCode);
        var active = (await client.GetFromJsonAsync<PagedResult<ProcedureManagerSummaryDto>>($"{Manager}?search={prefix}&categoryId=1&isActive=true"))!;
        Assert.Equal(3, active.TotalCount);
        var title = Uri.EscapeDataString($"  {prefix} ĐĂNG KÝ THỬ NGHIỆM 5  ");
        var byTitle = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?search={title}&categoryId=2"))!;
        Assert.Equal("Đất đai", Assert.Single(byTitle.Items).CategoryName);
        var empty = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?search={prefix}&categoryId=2147483647"))!;
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalPages);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task SearchTreatsWildcardsLiterally(string symbol)
    {
        var prefix = await Seed();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
            var row = ProcedureSeed.CreateSample();
            row.Id = Guid.NewGuid(); row.ProcedureCode = prefix + symbol; row.Title = "Thủ tục ký tự đặc biệt";
            db.Procedures.Add(row);
            await db.SaveChangesAsync();
        }
        using var client = fixture.Factory.CreateClient();
        var result = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"{Public}?search={Uri.EscapeDataString(prefix + symbol)}"))!;
        Assert.Equal(prefix + symbol, Assert.Single(result.Items).ProcedureCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=-1")]
    [InlineData("categoryId=0")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=wrong")]
    public async Task InvalidQueryReturnsProblemDetailsOnBothEndpoints(string query)
    {
        using var client = fixture.ManagerClient();
        foreach (var path in new[] { Public, Manager })
        {
            using var response = await client.GetAsync($"{path}?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
            Assert.True(problem.TryGetProperty("errors", out _));
        }
    }

    [Fact]
    public async Task ManagerListRequiresManagerOrAdminRole()
    {
        using var anonymous = fixture.Factory.CreateClient();
        using var citizen = fixture.ManagerClient("REGISTERED_CITIZEN");
        using var admin = fixture.ManagerClient("IT_ADMIN");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Manager)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.GetAsync(Manager)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Manager)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(Public)).StatusCode);
    }
}


