using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationDirectory(HttpClient http) : INotificationDirectory
{
    public async Task<NotificationAccess?> GetAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/access-context");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotificationAccess>(cancellationToken: ct);
    }
}
