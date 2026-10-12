using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public interface ISmsSender
{
    Task<string> Send(Notification notification, string phoneNumber, Guid deliveryId, CancellationToken ct);
}

// Explicitly blocked until a Brandname provider and its request/response contract are selected.
public sealed class UnconfiguredSmsSender : ISmsSender
{
    public Task<string> Send(Notification notification, string phoneNumber, Guid deliveryId, CancellationToken ct) => Task.FromResult("Blocked");
}

public sealed class ExternalNotificationSender(IConfiguration configuration, ISmsSender sms)
{
    public async Task<string> Send(Notification notification, RecipientContact contact, NotificationDelivery delivery, CancellationToken ct)
    {
        if (delivery.Channel == "SMS")
        {
            if (!configuration.GetValue<bool>("Sms:Enabled") || string.IsNullOrWhiteSpace(contact.PhoneNumber)) return "Blocked";
            return await sms.Send(notification, contact.PhoneNumber, delivery.Id, ct);
        }
        if (!configuration.GetValue<bool>("Email:Enabled")) return "Blocked";
        var host = configuration["Email:Host"]; var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(contact.Email)) return "Blocked";
        using var client = new SmtpClient(host, configuration.GetValue("Email:Port", 587))
        {
            EnableSsl = true, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(configuration["Email:Username"], configuration["Email:Password"]),
            Timeout = 15000
        };
        using var mail = new MailMessage(from, contact.Email, notification.Title, notification.Content) { IsBodyHtml = false };
        mail.Headers.Add("X-WardMate-Delivery-Id", delivery.Id.ToString());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await client.SendMailAsync(mail, deadline.Token);
        return "Sent"; // SMTP server accepted; this is not confirmation the email reached the inbox.
    }
}
