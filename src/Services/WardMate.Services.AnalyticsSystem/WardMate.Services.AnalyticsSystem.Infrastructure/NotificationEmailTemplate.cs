using System.Net;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public static class NotificationEmailTemplate
{
    public static (string Html, string Text) Render(Notification n, EmailContext? detail, string? citizenName, string? frontend)
    {
        static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "Chưa có thông tin" : value;
        static string Encode(string? value) => WebUtility.HtmlEncode(Value(value));
        static string Time(DateTime value) => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm 'UTC+07:00'");
        var statusText = detail?.Status switch
        {
            "SUBMITTED" => "Hồ sơ đã được nộp thành công để kiểm tra giấy tờ. Đây chưa phải việc tiếp nhận chính thức tại quầy.",
            "NEED_REVISION" => "Cán bộ đề nghị sửa đổi, bổ sung giấy tờ. Vui lòng đọc ý kiến bên dưới, cập nhật hồ sơ và nộp lại để được kiểm tra.",
            "DOCUMENTS_READY" => "Giấy tờ đã sẵn sàng. Vui lòng tải các biểu mẫu đã chuẩn bị và mang giấy tờ gốc cùng các bản sao theo yêu cầu đến bộ phận Một cửa của phường. Đây không phải quyết định phê duyệt thủ tục hành chính.",
            "OFFICIALLY_RECEIVED" => "Hồ sơ đã được tiếp nhận tại quầy. Vui lòng lưu số biên nhận và theo dõi ngày hẹn trả kết quả bên dưới.",
            _ => n.Content
        };
        var updated = Time(detail?.UpdatedAtUtc ?? n.CreatedAt);
        var code = detail?.ApplicationCode ?? n.ApplicationId?.ToString();
        var notes = detail?.Status == "NEED_REVISION" ? Value(detail.Notes) : null;
        var receipt = detail?.Status == "OFFICIALLY_RECEIVED";
        var appointment = detail?.AppointmentDate is DateTime date ? Time(date) : "Chưa có ngày hẹn; vui lòng theo dõi hồ sơ hoặc liên hệ bộ phận Một cửa.";
        // Only same-origin HTTP(S) links are allowed; no javascript/data/protocol-relative redirects.
        string? action = null;
        if (Uri.TryCreate(frontend, UriKind.Absolute, out var root) && root.Scheme is "https" or "http" && root.UserInfo.Length == 0
            && Uri.TryCreate(root, n.ActionUrl, out var target) && !string.IsNullOrWhiteSpace(n.ActionUrl)
            && target.Scheme == root.Scheme && target.Host == root.Host && target.Port == root.Port && target.UserInfo.Length == 0)
            action = target.AbsoluteUri;
        var rows = new List<(string Label, string Value)>
        {
            ("Mã hồ sơ", Value(code)), ("Tên thủ tục hành chính", Value(detail?.ProcedureTitle)),
            ("Tên công dân", Value(citizenName)), ("Thời gian cập nhật", updated)
        };
        if (receipt) { rows.Add(("Số biên nhận", Value(detail?.ReceiptNumber))); rows.Add(("Ngày hẹn trả kết quả", appointment)); }
        var table = string.Join("", rows.Select(r => $"<tr><td style='padding:12px;border-bottom:1px solid #e2e8f0;color:#475569;width:35%;vertical-align:top'>{Encode(r.Label)}</td><td style='padding:12px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-weight:600;word-break:break-word'>{Encode(r.Value)}</td></tr>"));
        var comment = notes is null ? "" : $"<div style='margin:24px 0;padding:18px;background:#fff7ed;border-left:4px solid #ea580c;border-radius:8px'><strong>Ý kiến của cán bộ</strong><p style='margin:10px 0 0;white-space:pre-wrap'>{Encode(notes)}</p></div>";
        var cta = action is null ? "<p>Vui lòng đăng nhập cổng WardMate để xem chi tiết hồ sơ.</p>" : $"<p style='margin:28px 0'><a href='{Encode(action)}' style='display:inline-block;background:#0f766e;color:#ffffff;text-decoration:none;font-weight:bold;padding:14px 24px;border-radius:8px'>Xem chi tiết hồ sơ</a></p>";
        var html = $$"""
<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{{Encode(n.Title)}}</title>
<style>@media(max-width:600px){.content{padding:20px!important}.shell{width:100%!important} }</style></head>
<body style="margin:0;background:#f1f5f9;font-family:Arial,Helvetica,sans-serif;color:#1e293b;line-height:1.6">
<table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td align="center" style="padding:24px 12px">
<table role="presentation" class="shell" width="640" cellspacing="0" cellpadding="0" style="width:100%;max-width:640px;background:#ffffff;border:1px solid #e2e8f0;border-radius:16px;overflow:hidden">
<tr><td style="padding:28px 32px;background:#115e59;color:#ffffff"><div style="font-size:28px;font-weight:bold;letter-spacing:1px">WardMate</div><div style="font-size:14px;margin-top:6px">Hệ thống Dịch vụ công Một cửa điện tử WardMate</div></td></tr>
<tr><td class="content" style="padding:32px"><h1 style="margin:0 0 16px;font-size:24px;line-height:1.35;color:#0f172a">{{Encode(n.Title)}}</h1>
<p>{{Encode(statusText)}}</p><table width="100%" cellspacing="0" cellpadding="0" style="font-size:14px;border:1px solid #e2e8f0;border-radius:8px">{{table}}</table>{{comment}}{{cta}}
<p style="font-size:13px;color:#64748b">Cần hỗ trợ? Vui lòng liên hệ bộ phận Một cửa của phường tiếp nhận hồ sơ.</p></td></tr>
<tr><td style="padding:20px 32px;background:#f8fafc;border-top:1px solid #e2e8f0;font-size:12px;color:#64748b">Email được gửi tự động từ WardMate. Không cung cấp mật khẩu hoặc mã OTP qua email. Thông tin hồ sơ dành cho người nhận; vui lòng không chuyển tiếp cho người không liên quan.</td></tr>
</table></td></tr></table></body></html>
""";
        var text = "Hệ thống Dịch vụ công Một cửa điện tử WardMate\n\n" + n.Title + "\n\n" + statusText + "\n\n"
            + string.Join("\n", rows.Select(r => r.Label + ": " + r.Value))
            + (notes is null ? "" : "\n\nÝ kiến của cán bộ:\n" + notes)
            + "\n\nXem chi tiết hồ sơ: " + (action ?? "Đăng nhập cổng WardMate")
            + "\n\nHỗ trợ: Liên hệ bộ phận Một cửa của phường. Email tự động; không cung cấp mật khẩu/OTP và không chuyển tiếp thông tin hồ sơ.";
        return (html, text);
    }
}

