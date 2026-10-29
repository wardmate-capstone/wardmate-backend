using System.Net.Http.Headers;
using System.Net.Http.Json;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class DispatchDirectory(HttpClient http) : IDispatchDirectory
{
    public async Task<DispatchOfficer[]> Officers(string token, CancellationToken ct)
    {
        var result = new List<DispatchOfficer>();
        for (var page = 1; page <= 100; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/manager/officers?page={page}&pageSize=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var batch = await response.Content.ReadFromJsonAsync<DispatchOfficerPage>(cancellationToken: ct)
                ?? throw new HttpRequestException("IAM trả danh sách rỗng.");
            if (batch.Items is null || batch.Items.Any(x => x.Roles is null || x.AssignedCategories is null))
                throw new HttpRequestException("IAM trả dữ liệu không hợp lệ.");
            result.AddRange(batch.Items);
            if (result.Count >= batch.Total) return result.DistinctBy(x => x.Id).ToArray();
            if (batch.Items.Length == 0) break;
        }
        throw new HttpRequestException("Không thể đọc đầy đủ danh sách cán bộ.");
    }
}
