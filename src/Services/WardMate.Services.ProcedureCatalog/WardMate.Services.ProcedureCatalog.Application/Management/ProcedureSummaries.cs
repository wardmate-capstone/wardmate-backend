using System.Globalization;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

/// <summary>
/// Tóm tắt lệ phí/thời hạn hiển thị ở danh sách. Khi người nhập để trống, sinh từ bước
/// "Thời hạn &amp; lệ phí" (contentPayload.submissionMethods) thay vì bắt nhập lại cùng thông tin.
/// BE-H (B.1) — thứ tự ưu tiên trong mỗi cách nộp: văn bản dự phòng (feeText / processingTime.text),
/// rồi số có cấu trúc (fees / processingTime), rồi trường cũ (feeAmount / estimatedDays).
/// Không còn cắt 255 ký tự (cột là text).
/// </summary>
public static class ProcedureSummaries
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static bool HasMethods(ProcedureContentPayload? payload) =>
        payload?.SubmissionMethods is { Count: > 0 } methods && methods.All(x => x is not null);

    public static string? Fee(string? summary, ProcedureContentPayload? payload)
    {
        if (!string.IsNullOrWhiteSpace(summary)) return summary.Trim();
        if (!HasMethods(payload)) return null;
        var texts = new List<string>();
        var amounts = new List<(decimal Amount, string Unit)>();
        var percents = new List<decimal>();
        foreach (var m in payload!.SubmissionMethods)
        {
            if (!string.IsNullOrWhiteSpace(m.FeeText)) { texts.Add(m.FeeText.Trim()); continue; }
            if (m.Fees is { Count: > 0 } fees)
            {
                foreach (var f in fees.Where(x => x is not null))
                {
                    if (f.Amount is { } a) amounts.Add((a, Unit(f.Unit)));
                    if (f.Percent is { } p) percents.Add(p);
                }
                continue;
            }
            if (m.FeeAmount is { } amount) amounts.Add((amount, Unit(m.FeeUnit)));
        }
        var parts = new List<string>();
        if (amounts.Count > 0 && percents.Count == 0 && amounts.All(x => x.Amount == 0)) parts.Add("Miễn phí");
        else
        {
            parts.AddRange(amounts.GroupBy(x => x.Unit, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"{Range(g.Min(x => x.Amount), g.Max(x => x.Amount))} {g.Key}"));
            if (percents.Count > 0) parts.Add($"{Range(percents.Min(), percents.Max())}%");
        }
        parts.AddRange(texts.Distinct(StringComparer.Ordinal));
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    public static string? ProcessingTime(string? summary, ProcedureContentPayload? payload)
    {
        if (!string.IsNullOrWhiteSpace(summary)) return summary.Trim();
        if (!HasMethods(payload)) return null;
        var texts = new List<string>();
        var values = new List<(decimal Value, string Unit)>();
        foreach (var m in payload!.SubmissionMethods)
        {
            var time = m.ProcessingTime;
            if (!string.IsNullOrWhiteSpace(time?.Text)) { texts.Add(time.Text.Trim()); continue; }
            if (time?.Value is { } value) { values.Add((value, time.Unit ?? "DAY")); continue; }
            if (m.EstimatedDays is { } days) values.Add((days, "DAY"));
        }
        var parts = new List<string>();
        // Giữ quy tắc cũ: mọi cách nộp 0 ngày → "Trong ngày".
        if (values.Count > 0 && values.All(x => x.Value == 0 && x.Unit is "DAY" or "WORKING_DAY")) parts.Add("Trong ngày");
        else parts.AddRange(values.GroupBy(x => x.Unit).OrderBy(g => UnitOrder(g.Key))
            .Select(g => $"{Range(g.Min(x => x.Value), g.Max(x => x.Value))} {UnitLabel(g.Key)}"));
        parts.AddRange(texts.Distinct(StringComparer.Ordinal));
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string Unit(string? unit) => string.IsNullOrWhiteSpace(unit) ? "VND" : unit.Trim();

    private static int UnitOrder(string unit) => unit switch { "WORKING_DAY" => 0, "DAY" => 1, "HOUR" => 2, _ => 3 };

    private static string UnitLabel(string unit) => unit switch
    {
        "WORKING_DAY" => "ngày làm việc",
        "HOUR" => "giờ",
        "MINUTE" => "phút",
        _ => "ngày"
    };

    private static string Range(decimal min, decimal max) =>
        min == max ? Number(min) : $"{Number(min)} – {Number(max)}";

    private static string Number(decimal value) => value.ToString("#,##0.##", Vi);
}
