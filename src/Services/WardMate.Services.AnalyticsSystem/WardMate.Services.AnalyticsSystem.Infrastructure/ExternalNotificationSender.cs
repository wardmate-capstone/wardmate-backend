using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public interface ISmsSender
{
    Task<SmsDeliveryResult> Send(Notification notification, string phoneNumber, Guid deliveryId, CancellationToken ct);
}
public sealed record SmsDeliveryResult(bool Success, string Status, string? ProviderMessageId, string? ErrorCode);

public sealed class NotificationSendException(string code, Exception? inner = null) : Exception(code, inner)
{
    public string Code { get; } = code;
}
public sealed class ExternalNotificationSender(IConfiguration configuration, ISmsSender sms, NotificationEmailSource source)
{
    public async Task<string> Send(Notification notification, RecipientContact contact, NotificationDelivery delivery, CancellationToken ct)
    {
        if (delivery.Channel == "SMS")
        {
            delivery.Provider = "eSMS";
            delivery.IsSandbox = (configuration["Sms:Sandbox"] ?? "1") != "0";
            if (!configuration.GetValue<bool>("Sms:Enabled") || string.IsNullOrWhiteSpace(contact.PhoneNumber))
            { delivery.ErrorCode = "sms.disabled_or_phone_missing"; return "Blocked"; }
            var result = await sms.Send(notification, contact.PhoneNumber, delivery.Id, ct);
            delivery.ExternalMessageId = result.ProviderMessageId;
            delivery.ErrorCode = result.ErrorCode;
            if (result.Success && result.Status is "Sent" or "Sandbox") return result.Status;
            if (!result.Success && result.Status is "Blocked" or "Unknown") return result.Status;
            throw new NotificationSendException(result.ErrorCode ?? "notification.sms_failed");
        }
        if (!configuration.GetValue<bool>("Email:Enabled")) return "Blocked";
        var host = configuration["Email:Host"]; var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(contact.Email)
            || string.IsNullOrWhiteSpace(configuration["Email:Username"]) || string.IsNullOrWhiteSpace(configuration["Email:Password"])) return "Blocked";
        var (context, citizenName) = await source.Read(notification, ct);
        var body = NotificationEmailTemplate.Render(notification, context, citizenName, configuration["Email:FrontendBaseUrl"]);
        using var client = new SmtpClient(host, configuration.GetValue("Email:Port", 587))
        {
            EnableSsl = true, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(configuration["Email:Username"], configuration["Email:Password"]), Timeout = 15000
        };
        using var mail = new MailMessage
        {
            From = new MailAddress(from, "Hệ thống Một cửa WardMate", Encoding.UTF8),
            Subject = notification.Title.Replace('\r', ' ').Replace('\n', ' '), SubjectEncoding = Encoding.UTF8,
            Body = body.Html, BodyEncoding = Encoding.UTF8, IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(contact.Email));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(body.Text, Encoding.UTF8, MediaTypeNames.Text.Plain));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(body.Html, Encoding.UTF8, MediaTypeNames.Text.Html));
        mail.Headers.Add("X-WardMate-Delivery-Id", delivery.Id.ToString());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { await client.SendMailAsync(mail, deadline.Token); }
        catch (SmtpException e) { throw new NotificationSendException("notification.smtp_failed", e); }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        { throw new NotificationSendException("notification.smtp_timeout", e); }
        return "Sent"; // Accepted by SMTP; not proof of inbox delivery. SMTP has no exactly-once guarantee.
    }
}
