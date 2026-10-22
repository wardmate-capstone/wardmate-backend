using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed record EmailContext(Guid ApplicantId, string ApplicationCode, string ProcedureTitle, string Status,
    string? Notes, string? ReceiptNumber, DateTime? AppointmentDate, DateTime UpdatedAtUtc);
public sealed class NotificationEmailSource(HttpClient http, IConfiguration config, NotificationDbContext db, IRecipientContacts contacts)
{
    public async Task<(EmailContext? Context, string? CitizenName)> Read(Notification notification, CancellationToken ct)
    {
        if (notification.SourceEventId is not Guid eventId || notification.ApplicationId is not Guid applicationId) return (null, null);
        var payload = await db.Inbox.AsNoTracking().Where(x => x.Id == eventId).Select(x => x.Payload).SingleOrDefaultAsync(ct);
        if (payload is null) throw new InvalidOperationException("notification.event_missing");
        var e = JsonSerializer.Deserialize<WorkflowEvent>(payload);
        if (e is null || e.ApplicationId != applicationId) throw new InvalidOperationException("notification.event_invalid");
        if (e.Kind != "STATUS" || e.Status is not ("SUBMITTED" or "NEED_REVISION" or "DOCUMENTS_READY" or "OFFICIALLY_RECEIVED")) return (null, null);
        var key = config["Analytics:ServiceKey"] ?? "";
        if (System.Text.Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("notification.workflow_key_missing");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/notifications/v1/applications/{applicationId}/events/{eventId}/email-context");
        request.Headers.Add("X-Analytics-Key", key);
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<EmailContext>(cancellationToken: ct);
        if (detail is null || detail.ApplicantId != e.ApplicantId || detail.Status != e.Status)
            throw new InvalidOperationException("notification.context_invalid");
        var citizen = (await contacts.Find([e.ApplicantId], null, ct)).SingleOrDefault();
        return (detail, citizen?.FullName);
    }
}
