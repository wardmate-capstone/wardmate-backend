using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Configuration;
using WardMate.Services.IAM.Application.Commands;
namespace WardMate.Services.IAM.Infrastructure.Security;

public sealed class IamEmailSender(IConfiguration config) : IIamEmailSender
{
    public bool Enabled => config.GetValue<bool>("Email:Enabled") && new[] { "Host", "From", "Username", "Password" }
        .All(k => !string.IsNullOrWhiteSpace(config["Email:" + k]));
    private static string E(string text) => WebUtility.HtmlEncode(text);
    private static string Shell(string title, string content) => $$"""
<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="margin:0;background:#f1f5f9;font-family:Arial,sans-serif;color:#0f172a;line-height:1.6"><table role="presentation" width="100%"><tr><td align="center" style="padding:24px 12px"><table role="presentation" width="600" style="width:100%;max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden" cellspacing="0"><tr><td style="padding:24px;background:#115e59;color:white;font-size:24px;font-weight:bold">WardMate</td></tr><tr><td style="padding:28px"><h1 style="font-size:22px">{{E(title)}}</h1>{{content}}</td></tr><tr><td style="padding:20px;background:#f8fafc;font-size:12px;color:#64748b">Email tự động từ Hệ thống Một cửa WardMate. Không chia sẻ mật khẩu hoặc OTP với bất kỳ ai. Nếu không thực hiện yêu cầu, hãy bỏ qua email này.</td></tr></table></td></tr></table></body></html>
""";
    public Task Welcome(string email, string fullName, string username, CancellationToken ct)
    {
        var login = config["Email:LoginUrl"];
        var cta = Uri.TryCreate(login, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0
            ? $"<p><a href='{E(uri.AbsoluteUri)}' style='display:inline-block;padding:14px 20px;border-radius:8px;background:#115e59;color:white;text-decoration:none'>Đăng nhập Cổng Dịch vụ công</a></p>" : "<p>Vui lòng mở cổng WardMate để đăng nhập.</p>";
        var body = $"<p>Chúc mừng <strong>{E(fullName)}</strong>!</p><p>Tài khoản công dân của bạn đã được tạo thành công.</p><p><strong>Tên đăng nhập:</strong> {E(username)}<br><strong>Email đăng ký:</strong> {E(email)}</p><p>Bạn có thể tra cứu thủ tục, chuẩn bị biểu mẫu và giấy tờ, gửi hồ sơ để cán bộ kiểm tra và theo dõi yêu cầu bổ sung.</p>{cta}";
        return Send(email, "Chào mừng bạn đến với WardMate", Shell("Chào mừng bạn đến với WardMate", body),
            $"Chúc mừng {fullName}! Tài khoản WardMate đã được tạo.\nTên đăng nhập: {username}\nEmail: {email}\nĐăng nhập cổng WardMate để tra cứu thủ tục, chuẩn bị giấy tờ và theo dõi hồ sơ.", ct);
    }
    public Task Otp(string email, string code, CancellationToken ct) => Send(email, "Mã OTP đổi mật khẩu WardMate",
        Shell("Xác nhận đổi mật khẩu", $"<p>Mã OTP của bạn:</p><div style='text-align:center;background:#ecfdf5;padding:20px;font-size:32px;font-weight:bold;color:#115e59;letter-spacing:8px'>{E(code)}</div><p>Mã có hiệu lực <strong>5 phút</strong>, chỉ dùng một lần. Gửi lại mã sẽ vô hiệu mã cũ.</p><p>Không chia sẻ mã này. WardMate không yêu cầu bạn cung cấp OTP qua điện thoại.</p>"),
        $"Mã OTP WardMate: {code}\nCó hiệu lực 5 phút, dùng một lần. Không chia sẻ mã. Nếu không yêu cầu, bỏ qua email này.", ct);
    private async Task Send(string to, string subject, string html, string text, CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("IAM email is not configured.");
        using var smtp = new SmtpClient(config["Email:Host"], config.GetValue("Email:Port", 587))
        {
            UseDefaultCredentials = false, EnableSsl = true, Timeout = 15000,
            Credentials = new NetworkCredential(config["Email:Username"], config["Email:Password"])
        };
        using var mail = new MailMessage
        {
            From = new MailAddress(config["Email:From"]!, config["Email:DisplayName"] ?? "Hệ thống Một cửa WardMate", Encoding.UTF8),
            Subject = subject, SubjectEncoding = Encoding.UTF8, Body = html, BodyEncoding = Encoding.UTF8, IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(to));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, Encoding.UTF8, MediaTypeNames.Text.Plain));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        await smtp.SendMailAsync(mail, deadline.Token);
    }
}
