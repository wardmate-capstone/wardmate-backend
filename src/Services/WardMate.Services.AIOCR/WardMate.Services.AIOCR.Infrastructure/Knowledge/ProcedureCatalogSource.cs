using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AIOCR.Application;

namespace WardMate.Services.AIOCR.Infrastructure.Knowledge;

// Reads published procedures through ProcedureCatalog's public HTTP API; never its database.
public sealed class ProcedureCatalogSource(HttpClient http, IConfiguration config) : IProcedureCatalogSource
{
    private const int PageSize = 50; // Catalog caps public pages at 50.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CatalogProcedure?> Get(Guid procedureId, CancellationToken ct)
    {
        using var response = await Send($"api/v1/procedures/{procedureId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        Ensure(response);
        var procedure = await response.Content.ReadFromJsonAsync<CatalogProcedure>(Json, ct);
        if (procedure is null || procedure.Id != procedureId || string.IsNullOrWhiteSpace(procedure.Title))
            throw new AiFailure(502, "knowledge.catalog_invalid_response", "Procedure Catalog trả dữ liệu thủ tục không hợp lệ.");
        return procedure.IsActive ? procedure : null;
    }

    public async Task<CatalogListing> ListActive(int max, CancellationToken ct)
    {
        var ids = new List<Guid>();
        for (var page = 1; ; page++)
        {
            using var response = await Send($"api/v1/procedures?pageNumber={page}&pageSize={PageSize}&sortBy=Title&isAscending=true", ct);
            Ensure(response);
            var result = await response.Content.ReadFromJsonAsync<CatalogPage>(Json, ct)
                ?? throw new AiFailure(502, "knowledge.catalog_invalid_response", "Procedure Catalog trả danh sách rỗng.");
            ids.AddRange(result.Items.Select(x => x.Id));
            if (ids.Count >= max) return new(ids.Take(max).Distinct().ToArray(), ids.Count == max && result.TotalCount == max);
            if (result.Items.Length < PageSize || ids.Count >= result.TotalCount) return new(ids.Distinct().ToArray(), true);
        }
    }

    private async Task<HttpResponseMessage> Send(string path, CancellationToken ct)
    {
        if (!Uri.TryCreate(config["Services:ProcedureCatalog:BaseUrl"], UriKind.Absolute, out var root)
            || root.Scheme is not ("https" or "http") || root.UserInfo.Length != 0 || root.Query.Length != 0 || root.Fragment.Length != 0)
            throw new AiFailure(503, "knowledge.catalog_not_configured", "Chưa cấu hình Services:ProcedureCatalog:BaseUrl.");
        try { return await http.GetAsync(new Uri(root, path), ct); }
        catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new AiFailure(503, "knowledge.catalog_unavailable", "Không kết nối được Procedure Catalog. Vui lòng thử lại."); }
    }

    private static void Ensure(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new AiFailure(503, "knowledge.catalog_unavailable", $"Procedure Catalog trả lỗi HTTP {(int)response.StatusCode}.");
    }

    private sealed record CatalogPage(CatalogItem[] Items, int TotalCount);
    private sealed record CatalogItem(Guid Id);
}
