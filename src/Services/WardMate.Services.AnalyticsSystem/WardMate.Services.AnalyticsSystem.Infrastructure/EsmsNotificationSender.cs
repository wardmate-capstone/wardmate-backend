using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

/// <summary>Customer-care SMS only. Templates must be approved for the configured Brandname.</summary>
public sealed class EsmsNotificationSender(HttpClient http, IConfiguration config, NotificationDbContext db) : ISmsSender
{
    public static string? NormalizePhone(string phone)
    {
        var value = Regex.Replace(phone.Trim(), @"[\s().-]", "");
        if (value.StartsWith("+84", StringComparison.Ordinal)) value = "0" + value[3..];
        else if (value.StartsWith("84", StringComparison.Ordinal)) value = "0" + value[2..];
        return Regex.IsMatch(value, "^0[35789][0-9]{8}$") ? value : null;
    }
    private static SmsDeliveryResult Stop(string code) => new(false, "Blocked", null, code);
    private static SmsDeliveryResult Unknown(string code) => new(false, "Unknown", null, code);
    public async Task<SmsDeliveryResult> Send(Notification notification, string phoneNumber, Guid deliveryId, CancellationToken ct)
    {
        var sandbox = config["Sms:Sandbox"] ?? "1";
        if (!config.GetValue<bool>("Sms:Enabled") || !string.Equals(config["Sms:Provider"], "eSMS", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(config["Sms:ApiKey"]) || string.IsNullOrWhiteSpace(config["Sms:SecretKey"])
            || !Regex.IsMatch(config["Sms:Brandname"] ?? "", "^[A-Za-z0-9 ]{1,32}$")
            || config["Sms:SmsType"] != "2" || sandbox is not ("0" or "1")) return Stop("sms.not_configured");
        if (sandbox == "0" && !config.GetValue<bool>("Sms:TemplatesApproved")) return Stop("sms.templates_not_approved");
        var phone = NormalizePhone(phoneNumber);
        if (phone is null) return Stop("sms.phone_invalid");
        if (notification.SourceEventId is not Guid eventId || notification.ApplicationId is not Guid applicationId)
            return Stop("sms.event_required");
        var payload = await db.Inbox.AsNoTracking().Where(x => x.Id == eventId).Select(x => x.Payload).SingleOrDefaultAsync(ct);
        var e = payload is null ? null : JsonSerializer.Deserialize<WorkflowEvent>(payload);
        if (e is null || e.EventId != eventId || e.ApplicationId != applicationId || !WorkflowNotificationConsumer.Valid(e))
            return Stop("sms.event_invalid");
        // Do not use free-text comments, citizen names or identity numbers in SMS.
        var key = notification.RecipientUserId == e.ApplicantId
            ? e.Kind == "COMMENT" ? "INLINE_COMMENT_ADDED" : e.Status
            : notification.RecipientWardCode == e.WardCode ? "OFFICER_" + e.Status : "";
        if (string.IsNullOrEmpty(key)) return Stop("sms.recipient_mismatch");
        var template = config[$"Sms:Templates:{key}"];
        if (string.IsNullOrWhiteSpace(template)) return Stop("sms.template_missing");
        if (!Regex.IsMatch(e.ApplicationCode, "^[A-Za-z0-9_-]{1,80}$")) return Stop("sms.application_code_invalid");
        var content = template.Replace("{ApplicationCode}", e.ApplicationCode, StringComparison.Ordinal);
        // ASCII customer-care templates: prevent unrecognized placeholders / accidental multi-part floods.
        if (content.Length is < 1 or > 459 || content.Any(c => c < 32 || c > 126) || content.Contains('{') || content.Contains('}'))
            return Stop("sms.template_invalid");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.PostAsJsonAsync(
                "https://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/",
                new { ApiKey = config["Sms:ApiKey"], SecretKey = config["Sms:SecretKey"], Phone = phone,
                    Content = content, Brandname = config["Sms:Brandname"], SmsType = "2", IsUnicode = "0",
                    Sandbox = sandbox, RequestId = deliveryId.ToString("N") },
                new JsonSerializerOptions { PropertyNamingPolicy = null }, timeout.Token);
            if (!response.IsSuccessStatusCode) return Unknown("sms.http_" + (int)response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (json.RootElement.ValueKind != JsonValueKind.Object) return Unknown("sms.invalid_response");
            var code = json.RootElement.TryGetProperty("CodeResult", out var c) ? c.ToString() : "";
            if (code != "100")
                return Regex.IsMatch(code, "^[0-9]{1,6}$") ? Stop("sms.provider_" + code) : Unknown("sms.invalid_response");
            var rawId = json.RootElement.TryGetProperty("SMSID", out var id) ? id.ToString() : "";
            var messageId = Regex.IsMatch(rawId, "^[A-Za-z0-9_-]{1,100}$") ? rawId : null;
            if (sandbox == "1") return new(true, "Sandbox", messageId, null);
            // Accepted without usable ID still must not be submitted again automatically.
            return new(true, "Sent", messageId, messageId is null ? "sms.accepted_without_id" : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            ct.ThrowIfCancellationRequested();
            return Unknown(ex is OperationCanceledException ? "sms.timeout_unknown" : "sms.transport_or_response_unknown");
        }
    }
}
