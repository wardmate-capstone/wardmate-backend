using System.Globalization;
using System.Text;

namespace WardMate.Services.AIOCR.Application;

// Renders a published catalog procedure as headed plain text so chunks follow its business sections.
// BE-H: ưu tiên văn bản dự phòng (feeText, processingTime.text, quantityText, stepsText) khi có; thêm điều kiện,
// cơ quan, kết quả có mã và các mục mở rộng (additionalSections) để chatbot trả lời đủ như văn bản gốc.
public static class ProcedureKnowledgeText
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Build(CatalogProcedure p)
    {
        var s = new StringBuilder();
        void Block(string? text) { if (!string.IsNullOrWhiteSpace(text)) s.Append(text.Trim()).Append("\n\n"); }
        void Field(List<string> lines, string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value.Trim()}"); }
        void ListField(List<string> lines, string label, string[]? values) => Field(lines, label, Join(values, "; "));
        var c = p.ContentPayload;

        Block($"# {p.Title}");
        var info = new List<string>();
        Field(info, "Mã thủ tục", p.ProcedureCode);
        Field(info, "Loại thủ tục", c?.ProcedureType);
        Field(info, "Lĩnh vực", p.CategoryName);
        Field(info, "Cấp thực hiện", p.LevelOfImplementation);
        Field(info, "Cơ quan có thẩm quyền", p.IssuingAuthority);
        Field(info, "Cơ quan thực hiện", p.ExecutingAgency);
        ListField(info, "Cơ quan được ủy quyền", c?.AuthorizedAgencies);
        ListField(info, "Cơ quan phối hợp", c?.CoordinatingAgencies);
        Field(info, "Đối tượng thực hiện", p.TargetAudience);
        Field(info, "Phí, lệ phí", p.FeeSummary);
        Field(info, "Thời hạn giải quyết theo thủ tục", p.ProcessingTimeSummary);
        Field(info, "Quyết định công bố", c?.DecisionNumber);
        Field(info, "Nơi tiếp nhận", c?.ReceivingAddresses is { Length: > 0 } ? Join(c.ReceivingAddresses, "; ") : c?.ReceivingAddress);
        ListField(info, "Từ khóa", c?.Keywords);
        Block(string.Join('\n', info));
        if (!string.IsNullOrWhiteSpace(c?.Description)) { Block("## Mô tả"); Block(c.Description); }
        if (!string.IsNullOrWhiteSpace(c?.Requirements)) { Block("## Yêu cầu, điều kiện thực hiện"); Block(c.Requirements); }

        // Hiển thị tên trường hợp thay cho mã khi thủ tục có khai báo.
        string CaseLabel(string code) => c?.Cases?.FirstOrDefault(k => k.CaseCode == code)?.CaseName is { Length: > 0 } name ? name : code;
        if (c?.SubmissionMethods is { Length: > 0 } methods)
        {
            Block("## Cách thức thực hiện");
            foreach (var m in methods)
            {
                var parts = new List<string> { MethodFee(m), MethodTime(m) }.Where(x => x.Length > 0).ToList();
                if (!string.IsNullOrWhiteSpace(m.CaseCode)) parts.Add($"trường hợp {CaseLabel(m.CaseCode)}");
                Block($"- {m.MethodName}{(parts.Count == 0 ? "" : ": " + string.Join(", ", parts))}.{(string.IsNullOrWhiteSpace(m.Note) ? "" : " " + m.Note.Trim())}");
            }
        }
        var commonSteps = c?.CommonSteps ?? [];
        if (!string.IsNullOrWhiteSpace(c?.StepsText) || commonSteps.Length > 0)
        {
            Block("## Trình tự thực hiện");
            Block(c?.StepsText);
            foreach (var step in commonSteps.OrderBy(x => x.StepOrder)) Block(Step(step));
        }
        if (c?.Cases is { Length: > 0 } cases)
        {
            Block("## Trường hợp và trình tự thực hiện");
            foreach (var item in cases)
            {
                Block($"### {item.CaseName}{(string.IsNullOrWhiteSpace(item.CaseCode) ? "" : $" ({item.CaseCode})")}");
                foreach (var step in (item.Steps ?? []).OrderBy(x => x.StepOrder)) Block(Step(step));
            }
        }
        var formNames = (p.FormDefinitions ?? []).Where(f => !string.IsNullOrWhiteSpace(f.FormCode))
            .GroupBy(f => f.FormCode!).ToDictionary(g => g.Key, g => g.First().FormName ?? g.Key);
        if (p.ChecklistSchema is { Length: > 0 } checklist)
        {
            Block("## Thành phần hồ sơ");
            foreach (var x in checklist)
            {
                var notes = new List<string> { x.IsMandatory ? "bắt buộc" : "không bắt buộc" };
                if (!string.IsNullOrWhiteSpace(x.QuantityText)) notes.Add(x.QuantityText.Trim());
                else if (x.CopyRequirements is { Length: > 0 } copies)
                    notes.Add(string.Join(" + ", copies.Where(k => k is not null).Select(k => $"{k.Quantity:00} {CopyType(k.DocumentCopyType ?? "")}")));
                else
                {
                    notes.Add($"số lượng {x.Quantity}");
                    if (!string.IsNullOrWhiteSpace(x.DocumentCopyType)) notes.Add(CopyType(x.DocumentCopyType));
                }
                if (!string.IsNullOrWhiteSpace(x.SubmissionType)) notes.Add(x.SubmissionType == "XUAT_TRINH" ? "xuất trình" : "nộp");
                if (!string.IsNullOrWhiteSpace(x.CaseCode)) notes.Add($"trường hợp {CaseLabel(x.CaseCode)}");
                if (x.FormCodes is { Length: > 0 } codes)
                    notes.Add("mẫu: " + string.Join(", ", codes.Select(code => formNames.GetValueOrDefault(code, code))));
                var tail = string.Join(" ", new[] { x.ConditionNote, x.Note }.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()));
                Block($"- {x.ItemName} ({string.Join("; ", notes)}).{(tail.Length == 0 ? "" : " " + tail)}");
            }
        }
        if (p.FormDefinitions is { Length: > 0 } forms)
        {
            Block("## Biểu mẫu");
            foreach (var f in forms)
                Block($"- {f.FormName}{(string.IsNullOrWhiteSpace(f.FormCode) ? "" : $" ({f.FormCode})")}: {(f.IsMandatory ? "bắt buộc" : "không bắt buộc")}, số lượng {f.Quantity}{(string.IsNullOrWhiteSpace(f.CaseCode) ? "" : $", trường hợp {CaseLabel(f.CaseCode)}")}.");
        }
        if (c?.ResultDetails is { Length: > 0 } details)
        {
            Block("## Kết quả thực hiện");
            foreach (var r in details.Where(x => !string.IsNullOrWhiteSpace(x?.Name)))
            {
                var extra = new[] { string.IsNullOrWhiteSpace(r.Code) ? null : $"mã {r.Code.Trim()}",
                    string.IsNullOrWhiteSpace(r.Validity) ? null : $"hiệu lực {r.Validity.Trim()}", r.Note?.Trim() }
                    .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                Block($"- {r.Name!.Trim()}{(extra.Length == 0 ? "" : $" ({string.Join("; ", extra)})")}");
            }
        }
        else if (c?.Results is { Length: > 0 } results)
        {
            Block("## Kết quả thực hiện");
            foreach (var r in results) Block($"- {r}");
        }
        if (c?.LegalReferences is { Length: > 0 } legal)
        {
            Block("## Căn cứ pháp lý");
            foreach (var l in legal)
                Block($"- {l.DocumentNumber} {l.DocumentName}".Trim() + (string.IsNullOrWhiteSpace(l.Authority) ? "" : $", {l.Authority}") + (l.IssueDate is { } d ? $", ban hành {d:dd/MM/yyyy}" : "") + ".");
        }
        foreach (var section in c?.AdditionalSections ?? [])
        {
            if (section is null || string.IsNullOrWhiteSpace(section.Title)) continue;
            Block($"## {section.Title.Trim()}");
            Block(section.Content);
        }
        return s.ToString();
    }

    private static string Step(CatalogStep step)
    {
        var name = string.IsNullOrWhiteSpace(step.StepName) ? "" : $" {step.StepName.Trim()}";
        var executor = string.IsNullOrWhiteSpace(step.Executor) ? "" : $" — thực hiện: {step.Executor.Trim()}";
        return $"Bước {step.StepOrder}.{name}{executor}. {step.ActionDetails}".Trim();
    }

    private static string MethodFee(CatalogMethod m)
    {
        if (!string.IsNullOrWhiteSpace(m.FeeText)) return $"phí, lệ phí: {m.FeeText.Trim()}";
        if (m.Fees is { Length: > 0 } fees)
        {
            var items = fees.Where(f => f is not null).Select(f =>
            {
                var value = f.Amount is { } a ? (a == 0 ? "miễn phí" : $"{Money(a)} {f.Unit ?? "VND"}".Trim())
                    : f.Percent is { } pct ? $"{pct.ToString("0.##", Vi)}%" : "theo quy định";
                var kind = f.Kind switch { "PHI" => "phí ", "LE_PHI" => "lệ phí ", _ => "" };
                return $"{kind}{value}{(string.IsNullOrWhiteSpace(f.Description) ? "" : $" ({f.Description.Trim()})")}";
            });
            return "phí, lệ phí: " + string.Join("; ", items);
        }
        if (m.FeeAmount is { } amount) return amount > 0 ? $"phí {Money(amount)} {m.FeeUnit}".TrimEnd() : "không thu phí";
        return "";
    }

    private static string MethodTime(CatalogMethod m)
    {
        if (!string.IsNullOrWhiteSpace(m.ProcessingTime?.Text)) return $"thời hạn {m.ProcessingTime.Text.Trim()}";
        if (m.ProcessingTime?.Value is { } value)
        {
            var unit = m.ProcessingTime.Unit switch { "WORKING_DAY" => "ngày làm việc", "HOUR" => "giờ", "MINUTE" => "phút", _ => "ngày" };
            return $"thời hạn {value.ToString("0.##", Vi)} {unit}";
        }
        return m.EstimatedDays is { } days && days > 0 ? $"thời gian dự kiến {days.ToString("0.##", Vi)} ngày" : "";
    }

    private static string Money(decimal value) => value.ToString("#,0.##", Vi);

    private static string? Join(string[]? values, string separator) =>
        values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() is { Length: > 0 } items ? string.Join(separator, items) : null;

    private static string CopyType(string value) => value switch
    {
        "ORIGINAL" => "bản chính",
        "REGULAR_COPY" => "bản sao",
        "CERTIFIED_COPY" => "bản sao chứng thực",
        "OTHER" => "loại bản khác",
        _ => value
    };
}
