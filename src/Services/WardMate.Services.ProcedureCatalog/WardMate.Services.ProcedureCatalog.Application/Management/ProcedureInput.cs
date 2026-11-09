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

    public void ApplyTo(Procedure procedure)
    {
        var content = NormalizedContent(ContentPayload);
        procedure.CategoryId = CategoryId;
        procedure.ProcedureCode = ProcedureCode.Trim();
        procedure.Title = Title.Trim();
        procedure.OriginalPdfUrl = OriginalPdfUrl;
        procedure.PdfFileName = PdfFileName;
        procedure.IssuingAuthority = Clean(IssuingAuthority);
        procedure.ExecutingAgency = Clean(ExecutingAgency);
        procedure.LevelOfImplementation = Clean(LevelOfImplementation);
        procedure.TargetAudience = Clean(TargetAudience);
        procedure.FeeSummary = ProcedureSummaries.Fee(FeeSummary, content);
        procedure.ProcessingTimeSummary = ProcedureSummaries.ProcessingTime(ProcessingTimeSummary, content);
        procedure.ContentPayload = content;
        procedure.ChecklistSchema = NormalizedChecklist(ChecklistSchema);
        procedure.FormDefinitions = FormDefinitions;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Nội dung null hoặc danh sách null được lưu thành object/mảng rỗng để client đọc ổn định.</summary>
    public static ProcedureContentPayload NormalizedContent(ProcedureContentPayload? payload)
    {
        payload ??= new();
        payload.SubmissionMethods ??= [];
        payload.LegalReferences ??= [];
        payload.Results ??= [];
        payload.Cases ??= [];
        foreach (var item in payload.Cases.Where(x => x is not null)) item.Steps ??= [];
        return payload;
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
