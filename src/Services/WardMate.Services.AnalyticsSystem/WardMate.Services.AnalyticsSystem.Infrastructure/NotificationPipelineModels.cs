using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationInbox
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = "";
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorCode { get; set; }
}

public sealed class NotificationDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = "IN_APP";
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public string? ErrorCode { get; set; }
    public DateTime? SentAt { get; set; }
}

public sealed record RecipientContact(Guid UserId, string Email, string? PhoneNumber, string? WardCode, bool IsOfficer);
public interface IRecipientContacts
{
    Task<RecipientContact[]> Find(Guid[] userIds, string? wardCode, CancellationToken ct);
}
public sealed class RecipientContacts(HttpClient http, IConfiguration configuration) : IRecipientContacts
{
    public async Task<RecipientContact[]> Find(Guid[] userIds, string? wardCode, CancellationToken ct)
    {
        var key = configuration["Notifications:ServiceKey"] ?? "";
        if (key.Length < 32) throw new InvalidOperationException("Chưa cấu hình khóa thông báo IAM.");
        var results = new List<RecipientContact>();
        for (var page = 1; page <= 10000; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/notification-recipients")
            { Content = JsonContent.Create(new { userIds, wardCode, page }) };
            request.Headers.Add("X-Notification-Service-Key", key);
            using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
            var batch = await response.Content.ReadFromJsonAsync<RecipientContact[]>(cancellationToken: ct)
                ?? throw new HttpRequestException("IAM trả dữ liệu rỗng.");
            results.AddRange(batch);
            if (batch.Length < 200) return results.DistinctBy(x => x.UserId).ToArray();
        }
        throw new InvalidOperationException("Danh sách người nhận vượt giới hạn.");
    }
}
