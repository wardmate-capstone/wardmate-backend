using System.Globalization;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

/// <summary>
/// Tóm tắt lệ phí/thời hạn hiển thị ở danh sách. Khi người nhập để trống, sinh từ bước
/// "Thời hạn &amp; lệ phí" (contentPayload.submissionMethods) thay vì bắt nhập lại cùng thông tin.
/// </summary>
public static class ProcedureSummaries
{
    private const int MaxLength = 255;
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static bool HasMethods(ProcedureContentPayload? payload) =>
        payload?.SubmissionMethods is { Count: > 0 } methods && methods.All(x => x is not null);

    public static string Fee(string? summary, ProcedureContentPayload? payload)
    {
        if (!string.IsNullOrWhiteSpace(summary) || !HasMethods(payload)) return summary?.Trim() ?? string.Empty;
        var methods = payload!.SubmissionMethods;
        if (methods.All(x => x.FeeAmount == 0)) return "Miễn phí";
        var parts = methods
            .GroupBy(x => string.IsNullOrWhiteSpace(x.FeeUnit) ? "VND" : x.FeeUnit.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{Range(g.Min(x => x.FeeAmount), g.Max(x => x.FeeAmount))} {g.Key}");
        return Clip(string.Join("; ", parts));
    }

    public static string ProcessingTime(string? summary, ProcedureContentPayload? payload)
    {
        if (!string.IsNullOrWhiteSpace(summary) || !HasMethods(payload)) return summary?.Trim() ?? string.Empty;
        var days = payload!.SubmissionMethods.Select(x => x.EstimatedDays).ToArray();
        var min = days.Min();
        var max = days.Max();
        if (max == 0) return "Trong ngày";
        return Clip($"{Range(min, max)} ngày");
    }

    private static string Range(decimal min, decimal max) =>
        min == max ? Number(min) : $"{Number(min)} – {Number(max)}";

    private static string Number(decimal value) => value.ToString("#,##0.##", Vi);

    private static string Clip(string value) => value.Length <= MaxLength ? value : value[..MaxLength];
}
