using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class AnalyticsWorkflowClient(HttpClient http, IConfiguration config) : IAnalyticsWorkflowClient
{
    public async Task<AnalyticsDay> ReadDay(DateOnly date, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/analytics/v1/daily?date={date:yyyy-MM-dd}");
        request.Headers.Add("X-Analytics-Key", config["Analytics:ServiceKey"]);
        return await Send<AnalyticsDay>(request, ct);
    }
    public async Task<FeedbackApplication> ReadOwned(Guid id, string bearerToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/applications/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return await Send<FeedbackApplication>(request, ct);
    }
    private async Task<T> Send<T>(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new AnalyticsException(404, "feedback.application_not_found", "Không tìm thấy hồ sơ trong phạm vi của bạn.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new AnalyticsException(403, "analytics.workflow_denied", "Workflow từ chối truy cập.");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                ?? throw new JsonException("Empty workflow response");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new AnalyticsException(503, "analytics.workflow_unavailable", "Chưa thể đọc dữ liệu Workflow. Vui lòng thử lại."); }
    }
}
