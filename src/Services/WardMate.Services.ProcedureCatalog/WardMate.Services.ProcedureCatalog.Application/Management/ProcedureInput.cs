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
        procedure.ChecklistSchema = ChecklistSchema;
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
        return payload;
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
