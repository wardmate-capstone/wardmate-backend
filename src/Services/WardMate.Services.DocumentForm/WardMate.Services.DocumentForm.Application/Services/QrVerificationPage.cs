using System.Globalization;
using System.Text.Encodings.Web;
using WardMate.Services.DocumentForm.Application.Queries;

namespace WardMate.Services.DocumentForm.Application.Services;

/// <summary>
/// DOC-QR-07: trang HTML công khai khi quét QR mà hồ sơ không có (hoặc mất) bản PDF.
/// Trước đây trình duyệt nhận JSON 404 thô. Trang chỉ hiện thông tin đã công khai trong QR
/// (mã hồ sơ nội bộ, thời điểm cấp mã); không hiện họ tên hay giấy tờ của công dân.
/// </summary>
public static class QrVerificationPage
{
    private static readonly TimeZoneInfo Vietnam = FindVietnam();
    // Giữ nguyên chữ tiếng Việt; vẫn mã hóa < > & " ' để chống chèn HTML.
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All);

    public static string Valid(QrLookupResultDto qr, bool pdfMissing) => Render(
        "Mã QR hợp lệ",
        QrLookupQueryHandler.QrValidMessage,
        [
            ("Mã tham chiếu", qr.ApplicationId.ToString()),
            ("Thời điểm cấp mã", ToVietnamTime(qr.GeneratedAt) + " (giờ Việt Nam)"),
        ],
        pdfMissing
            ? "Hồ sơ này không kèm bản PDF tờ khai (hoặc bản PDF không còn trên hệ thống). Cán bộ đối chiếu trực tiếp giấy tờ gốc người dân mang đến quầy."
            : null,
        ok: true);

    public static string NotFound() => Render(
        "Không tìm thấy hồ sơ",
        "Không tìm thấy hồ sơ tương ứng với mã QR này. Mã có thể đã bị sửa hoặc hồ sơ chưa được cán bộ xác nhận giấy tờ sẵn sàng.",
        [], null, ok: false);

    private static string Render(string title, string message, (string Label, string Value)[] rows, string? note, bool ok)
    {
        var e = Encoder;
        var rowsHtml = string.Concat(rows.Select(r => $"<tr><th>{e.Encode(r.Label)}</th><td>{e.Encode(r.Value)}</td></tr>"));
        var noteHtml = note is null ? "" : $"<p class=\"note\">{e.Encode(note)}</p>";
        var color = ok ? "#166534" : "#991b1b";
        return $$"""
            <!doctype html>
            <html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex"><title>{{e.Encode(title)}} – WardMate</title>
            <style>body{font-family:system-ui,Arial,sans-serif;margin:0;background:#f8fafc;color:#0f172a}
            main{max-width:560px;margin:32px auto;padding:24px;background:#fff;border-radius:12px;border:1px solid #e2e8f0}
            h1{font-size:20px;color:{{color}};margin:0 0 12px}p{line-height:1.5}table{width:100%;border-collapse:collapse;margin-top:12px}
            th,td{text-align:left;padding:8px;border-top:1px solid #e2e8f0;font-size:14px;vertical-align:top}th{width:40%;color:#475569;font-weight:600}
            .note{background:#fffbeb;border:1px solid #fde68a;padding:12px;border-radius:8px;font-size:14px}footer{margin-top:16px;font-size:12px;color:#64748b}</style>
            </head><body><main><h1>{{e.Encode(title)}}</h1><p>{{e.Encode(message)}}</p>
            <table>{{rowsHtml}}</table>{{noteHtml}}
            <footer>WardMate – hỗ trợ tiền kiểm hồ sơ hành chính cấp xã/phường. Mã QR không phải giấy hẹn, số thứ tự hay kết quả giải quyết thủ tục.</footer>
            </main></body></html>
            """;
    }

    private static string ToVietnamTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Vietnam)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static TimeZoneInfo FindVietnam()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
    }
}
