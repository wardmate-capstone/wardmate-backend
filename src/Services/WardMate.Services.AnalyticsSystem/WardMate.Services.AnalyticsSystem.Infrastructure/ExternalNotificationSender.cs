using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationSendException(string code, Exception? inner = null) : Exception(code, inner)
{
    public string Code { get; } = code;
}
public sealed class ExternalNotificationSender(IConfiguration configuration, NotificationEmailSource source)
{
    public async Task<string> Send(Notification notification, RecipientContact contact, NotificationDelivery delivery, CancellationToken ct)
    {
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
