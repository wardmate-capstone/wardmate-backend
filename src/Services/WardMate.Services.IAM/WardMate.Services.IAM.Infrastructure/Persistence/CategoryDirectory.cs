using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.IAM.Application.Accounts;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class CategoryDirectory(HttpClient http, IConfiguration config) : ICategoryDirectory
{
    public async Task<int[]> ReadIds(CancellationToken ct)
    {
        if (!Uri.TryCreate(config["ProcedureCatalog:BaseUrl"], UriKind.Absolute, out var root)
            || root.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(root.UserInfo)
            || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
            throw new HttpRequestException("Catalog address is not configured.");
        using var response = await http.GetAsync(new Uri(root.AbsoluteUri.TrimEnd('/') + "/api/v1/procedures/categories"), ct);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<CategoryReference[]>(cancellationToken: ct);
        if (rows is null || rows.Any(x => x is null || x.Id <= 0) || rows.Select(x => x.Id).Distinct().Count() != rows.Length)
            throw new HttpRequestException("Invalid full category directory.");
        return rows.Select(x => x.Id).ToArray();
    }
    private sealed record CategoryReference(int Id);
}
