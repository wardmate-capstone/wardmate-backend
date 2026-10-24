using System.Globalization;
using System.Text;

namespace WardMate.Services.AIOCR.Application;

// Renders a published catalog procedure as headed plain text so chunks follow its business sections.
public static class ProcedureKnowledgeText
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Build(CatalogProcedure p)
    {
        var s = new StringBuilder();
        void Block(string? text) { if (!string.IsNullOrWhiteSpace(text)) s.Append(text.Trim()).Append("\n\n"); }
        void Field(List<string> lines, string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value.Trim()}"); }
        var c = p.ContentPayload;

        Block($"# {p.Title}");
        var info = new List<string>();
        Field(info, "Mã thủ tục", p.ProcedureCode);
        Field(info, "Lĩnh vực", p.CategoryName);
        Field(info, "Cấp thực hiện", p.LevelOfImplementation);
        Field(info, "Cơ quan ban hành", p.IssuingAuthority);
        Field(info, "Cơ quan thực hiện", p.ExecutingAgency);
        Field(info, "Đối tượng thực hiện", p.TargetAudience);
        Field(info, "Phí, lệ phí", p.FeeSummary);
        Field(info, "Thời hạn giải quyết theo thủ tục", p.ProcessingTimeSummary);
        Field(info, "Quyết định công bố", c?.DecisionNumber);
        Field(info, "Nơi tiếp nhận", c?.ReceivingAddress);
        Block(string.Join('\n', info));

        if (c?.SubmissionMethods is { Length: > 0 } methods)
        {
            Block("## Cách thức thực hiện");
            foreach (var m in methods)
            {
                var fee = m.FeeAmount > 0 ? $"phí {m.FeeAmount.ToString("#,0.##", Vi)} {m.FeeUnit}".TrimEnd() : "không thu phí";
                var days = m.EstimatedDays > 0 ? $", thời gian dự kiến {m.EstimatedDays.ToString("0.##", Vi)} ngày" : "";
                Block($"- {m.MethodName}: {fee}{days}.{(string.IsNullOrWhiteSpace(m.Note) ? "" : " " + m.Note.Trim())}");
            }
        }
        if (c?.Cases is { Length: > 0 } cases)
        {
            Block("## Trường hợp và trình tự thực hiện");
            foreach (var item in cases)
            {
                Block($"### {item.CaseName}{(string.IsNullOrWhiteSpace(item.CaseCode) ? "" : $" ({item.CaseCode})")}");
                foreach (var step in (item.Steps ?? []).OrderBy(x => x.StepOrder))
                    Block($"Bước {step.StepOrder}. {step.StepName}{(string.IsNullOrWhiteSpace(step.Executor) ? "" : $" — thực hiện: {step.Executor}")}. {step.ActionDetails}".Trim());
            }
        }
        if (p.ChecklistSchema is { Length: > 0 } checklist)
        {
            Block("## Thành phần hồ sơ");
            foreach (var x in checklist)
            {
                var notes = new List<string> { x.IsMandatory ? "bắt buộc" : "không bắt buộc", $"số lượng {x.Quantity}" };
                if (!string.IsNullOrWhiteSpace(x.DocumentCopyType)) notes.Add(CopyType(x.DocumentCopyType));
                if (!string.IsNullOrWhiteSpace(x.SubmissionType)) notes.Add(x.SubmissionType == "XUAT_TRINH" ? "xuất trình" : "nộp");
                if (!string.IsNullOrWhiteSpace(x.CaseCode)) notes.Add($"trường hợp {x.CaseCode}");
                Block($"- {x.ItemName} ({string.Join("; ", notes)}).{(string.IsNullOrWhiteSpace(x.ConditionNote) ? "" : " " + x.ConditionNote.Trim())}");
            }
        }
        if (p.FormDefinitions is { Length: > 0 } forms)
        {
            Block("## Biểu mẫu");
            foreach (var f in forms)
                Block($"- {f.FormName}{(string.IsNullOrWhiteSpace(f.FormCode) ? "" : $" ({f.FormCode})")}: {(f.IsMandatory ? "bắt buộc" : "không bắt buộc")}, số lượng {f.Quantity}{(string.IsNullOrWhiteSpace(f.CaseCode) ? "" : $", trường hợp {f.CaseCode}")}.");
        }
        if (c?.Results is { Length: > 0 } results)
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
        return s.ToString();
    }

    private static string CopyType(string value) => value switch
    {
        "ORIGINAL" => "bản chính",
        "REGULAR_COPY" => "bản sao",
        "CERTIFIED_COPY" => "bản sao chứng thực",
        _ => value
    };
}
