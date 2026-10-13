using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class WorkflowDirectory(HttpClient http) : IWorkflowDirectory
{
    public async Task<WorkflowAccess?> Access(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/access-context");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowAccess>(cancellationToken: ct)
            ?? throw new HttpRequestException("IAM trả ngữ cảnh rỗng.");
    }
    public async Task<bool> WardExists(string code, CancellationToken ct)
    {
        var wards = await http.GetFromJsonAsync<WardReference[]>("api/v1/wards", ct)
            ?? throw new HttpRequestException("IAM trả danh sách phường rỗng.");
        return wards.Any(w => w.Code == code);
    }
    private sealed record WardReference(string Code);
}
