using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public class ProcedureInput
{
    public int CategoryId { get; set; }
    public string ProcedureCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? OriginalPdfUrl { get; set; }
    public string? PdfFileName { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? ExecutingAgency { get; set; }
    // PROC-01b: chỉ mã, tên, lĩnh vực bắt buộc; các trường dưới được null hoặc để trống.
    public string? LevelOfImplementation { get; set; }
    public string? TargetAudience { get; set; }
    // Để trống: BE tự sinh từ contentPayload.submissionMethods (ProcedureSummaries); không có cách nộp thì null.
    public string? FeeSummary { get; set; }
    public string? ProcessingTimeSummary { get; set; }
    public ProcedureContentPayload? ContentPayload { get; set; } = new();
    public List<ChecklistItemSchema>? ChecklistSchema { get; set; }
    public List<FormDefinitionSchema>? FormDefinitions { get; set; }

    /// <param name="keepSourceWhenMissing">
    /// PROC-01c: true cho sửa (PUT) và publish đè thủ tục đã có — không gửi <c>originalPdfUrl</c> (null) thì giữ PDF
    /// đang gắn cùng <c>pdfFileName</c>; gắn/thay PDF dùng POST .../source. false (tạo mới, rollback) gán đúng giá trị gửi lên.
    /// </param>
    public void ApplyTo(Procedure procedure, bool keepSourceWhenMissing = false)
    {
        var content = NormalizedContent(ContentPayload);
        procedure.CategoryId = CategoryId;
        procedure.ProcedureCode = ProcedureCode.Trim();
        procedure.Title = OneLine(Title);
        if (!keepSourceWhenMissing)
        {
            procedure.OriginalPdfUrl = OriginalPdfUrl;
            procedure.PdfFileName = PdfFileName;
        }
        else if (OriginalPdfUrl is not null)
        {
            // Gửi lại đúng URL đang gắn mà thiếu tên file thì giữ tên file cũ.
            if (OriginalPdfUrl != procedure.OriginalPdfUrl || PdfFileName is not null) procedure.PdfFileName = PdfFileName;
            procedure.OriginalPdfUrl = OriginalPdfUrl;
        }
        // BE-H (B.2): trường cũ trống thì suy ra từ danh sách mới để bộ lọc, Workflow, AIOCR và FE cũ vẫn đọc được.
        procedure.IssuingAuthority = Clean(IssuingAuthority) ?? Join(content.CompetentAuthorities, "; ");
        procedure.ExecutingAgency = Clean(ExecutingAgency) ?? Join(content.ExecutingAgencies, "; ");
        procedure.LevelOfImplementation = Clean(LevelOfImplementation) ?? Clip(Join(content.Levels, ", "), ProcedureLimits.Level);
        procedure.TargetAudience = Clean(TargetAudience);
        procedure.FeeSummary = ProcedureSummaries.Fee(FeeSummary, content);
        procedure.ProcessingTimeSummary = ProcedureSummaries.ProcessingTime(ProcessingTimeSummary, content);
        procedure.SearchKeywords = Join(content.Keywords, " | ");
        procedure.ContentPayload = content;
        procedure.ChecklistSchema = NormalizedChecklist(ChecklistSchema);
        procedure.FormDefinitions = FormDefinitions;
    }

    /// <summary>KB-TITLE-01: tên thủ tục luôn một dòng — gộp xuống dòng/tab/khoảng trắng liên tiếp thành một dấu cách.</summary>
    public static string OneLine(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? Clean(string? value) =>string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Join(List<string>? values, string separator) => values is { Count: > 0 } ? string.Join(separator, values) : null;
    private static string? Clip(string? value, int max) => value is null || value.Length <= max ? value : value[..max].TrimEnd();

    /// <summary>Bỏ phần tử rỗng, cắt khoảng trắng; danh sách rỗng sau khi lọc thành null.</summary>
    private static List<string>? CleanList(List<string>? values)
    {
        var cleaned = values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        return cleaned is { Count: > 0 } ? cleaned : null;
    }

    /// <summary>
    /// Nội dung null hoặc danh sách null (trường cũ) được lưu thành object/mảng rỗng để client đọc ổn định.
    /// BE-H: trường mới tuỳ chọn để null khi không có; chuỗi chỉ khoảng trắng thành null; suy ra trường cũ
    /// (results, receivingAddress, feeAmount, estimatedDays) để chỗ đọc cũ không phải sửa cùng lúc.
    /// </summary>
    public static ProcedureContentPayload NormalizedContent(ProcedureContentPayload? payload)
    {
        payload ??= new();
        payload.DecisionNumber ??= string.Empty;
        payload.ReceivingAddress ??= string.Empty;
        payload.SubmissionMethods ??= [];
        payload.LegalReferences ??= [];
        payload.Results ??= [];
        payload.Cases ??= [];
        foreach (var item in payload.Cases.Where(x => x is not null)) item.Steps = NormalizedSteps(item.Steps) ?? [];
        payload.CommonSteps = NormalizedSteps(payload.CommonSteps);
        payload.ProcedureType = Clean(payload.ProcedureType);
        payload.Requirements = Clean(payload.Requirements);
        payload.Description = Clean(payload.Description);
        payload.StepsText = Clean(payload.StepsText);
        payload.Levels = CleanList(payload.Levels);
        payload.CompetentAuthorities = CleanList(payload.CompetentAuthorities);
        payload.ExecutingAgencies = CleanList(payload.ExecutingAgencies);
        payload.AuthorizedAgencies = CleanList(payload.AuthorizedAgencies);
        payload.CoordinatingAgencies = CleanList(payload.CoordinatingAgencies);
        payload.ReceivingAddresses = CleanList(payload.ReceivingAddresses);
        payload.Keywords = CleanList(payload.Keywords);
        if (string.IsNullOrWhiteSpace(payload.ReceivingAddress) && payload.ReceivingAddresses is { } addresses)
            payload.ReceivingAddress = string.Join("; ", addresses);
        if (payload.ResultDetails is { Count: > 0 } details)
        {
            foreach (var d in details.Where(x => x is not null))
            {
                d.Name = (d.Name ?? string.Empty).Trim(); d.Code = Clean(d.Code); d.Validity = Clean(d.Validity); d.Note = Clean(d.Note);
            }
            payload.Results = details.Where(x => x is not null).Select(x => x.Name).ToList();
        }
        else payload.ResultDetails = null;
        if (payload.AdditionalSections is { Count: > 0 } sections)
            foreach (var s in sections.Where(x => x is not null)) { s.Title = (s.Title ?? string.Empty).Trim(); s.Content = Clean(s.Content); }
        else payload.AdditionalSections = null;
        foreach (var method in payload.SubmissionMethods.Where(x => x is not null)) NormalizeMethod(method);
        return payload;
    }

    private static void NormalizeMethod(SubmissionMethodDetail method)
    {
        method.FeeUnit = string.IsNullOrWhiteSpace(method.FeeUnit) ? "VND" : method.FeeUnit.Trim();
        method.FeeText = Clean(method.FeeText);
        method.CaseCode = Clean(method.CaseCode);
        if (method.Fees is { Count: > 0 } fees)
        {
            foreach (var fee in fees.Where(x => x is not null))
            {
                fee.Unit = string.IsNullOrWhiteSpace(fee.Unit) ? "VND" : fee.Unit.Trim();
                fee.Description = Clean(fee.Description);
                fee.CaseCode = Clean(fee.CaseCode);
            }
            // BE-H (B.1): feeAmount cũ = mức nhỏ nhất có số tiền.
            var amounts = fees.Where(x => x?.Amount is not null).Select(x => x!.Amount!.Value).ToArray();
            if (amounts.Length > 0) method.FeeAmount = amounts.Min();
        }
        else method.Fees = null;
        if (method.ProcessingTime is { } time)
        {
            time.Text = Clean(time.Text);
            if (time.Value is null && time.Unit is null && time.Text is null) method.ProcessingTime = null;
            // BE-H (B.1): estimatedDays cũ = số ngày khi thời hạn tính theo ngày.
            else if (time.Value is { } value && time.Unit is "DAY" or "WORKING_DAY") method.EstimatedDays = value;
        }
    }

    /// <summary>BE-H (B.4): tên/nội dung null thành chuỗi rỗng; thứ tự 0 (không gửi) tự đánh theo vị trí.</summary>
    private static List<ProcedureStepDetail>? NormalizedSteps(List<ProcedureStepDetail>? steps)
    {
        if (steps is not { Count: > 0 }) return null;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step is null) continue;
            step.StepName ??= string.Empty;
            step.Executor ??= string.Empty;
            step.ActionDetails ??= string.Empty;
            if (step.StepOrder <= 0) step.StepOrder = i + 1;
        }
        return steps;
    }

    /// <summary>
    /// PROC-02a: đồng bộ loại bản. Có CopyRequirements: 1 dòng → DocumentCopyType/Quantity theo dòng đó;
    /// nhiều dòng → DocumentCopyType = null (Workflow chấp nhận mọi loại bản) và Quantity = tổng.
    /// Không có CopyRequirements mà có DocumentCopyType → sinh 1 dòng để client luôn đọc được danh sách.
    /// </summary>
    public static List<ChecklistItemSchema>? NormalizedChecklist(List<ChecklistItemSchema>? items)
    {
        if (items is null) return null;
        foreach (var item in items.Where(x => x is not null))
        {
            if (item.CopyRequirements is { Count: > 0 } copies)
            {
                item.DocumentCopyType = copies.Count == 1 ? copies[0].DocumentCopyType : null;
                item.Quantity = copies.Sum(x => x.Quantity);
            }
            else
            {
                item.CopyRequirements = item.DocumentCopyType is null
                    ? null
                    : [new CopyRequirement { DocumentCopyType = item.DocumentCopyType, Quantity = item.Quantity }];
            }
            // BE-H (B.3): văn bản dự phòng và mẫu đơn gắn kèm; danh sách rỗng lưu thành null.
            item.QuantityText = Clean(item.QuantityText);
            item.Note = Clean(item.Note);
            item.FormCodes = item.FormCodes is { Count: > 0 } codes ? codes.Distinct(StringComparer.Ordinal).ToList() : null;
        }
        return items;
    }
}

/// <summary>Body tạo thủ tục nhập tay. Mặc định tạo ở trạng thái ẩn; gửi isActive=true để công khai ngay (PROC-03b).</summary>
public sealed class CreateProcedureInput : ProcedureInput
{
    public bool IsActive { get; set; }
}

public sealed class UpdateProcedureInput : ProcedureInput
{
    public string DecisionNumber { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
}
