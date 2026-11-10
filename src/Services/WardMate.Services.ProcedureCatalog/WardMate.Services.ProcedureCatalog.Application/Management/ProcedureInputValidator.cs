using System.Text.Encodings.Web;
using System.Text.Json;
using FluentValidation;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed class ProcedureInputValidator : AbstractValidator<ProcedureInput>
{
    private static readonly JsonSerializerOptions SizeOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly string Line = ProcedureLimits.Line.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    public ProcedureInputValidator()
    {
        RuleFor(x => x.OriginalPdfUrl).MaximumLength(500).WithMessage("URL PDF không được vượt quá 500 ký tự.")
            .Must(x => x is null || (Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)))
            .WithMessage("URL PDF phải là đường dẫn HTTPS tuyệt đối hoặc null.");
        RuleFor(x => x.PdfFileName).MaximumLength(255).WithMessage("Tên file PDF không được vượt quá 255 ký tự.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Danh mục phải là số nguyên lớn hơn 0.");
        RuleFor(x => x.ProcedureCode).NotEmpty().WithMessage("Mã thủ tục không được để trống.")
            .MaximumLength(ProcedureLimits.ProcedureCode).WithMessage("Mã thủ tục không được vượt quá 100 ký tự.");
        // PROC-01b: chỉ mã, tên, lĩnh vực bắt buộc; các trường còn lại chỉ kiểm độ dài khi có giá trị.
        // BE-H (A): văn bản mô tả chỉ còn trần chống dán nhầm (ProcedureLimits), không còn 255/500.
        RuleFor(x => x.Title).Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Tên thủ tục không được để trống.")
            .MaximumLength(ProcedureLimits.Title).WithMessage("Tên thủ tục không được vượt quá 1.000 ký tự.");
        RuleFor(x => x.IssuingAuthority).MaximumLength(ProcedureLimits.Line).WithMessage($"Cơ quan có thẩm quyền không được vượt quá {Line} ký tự.");
        RuleFor(x => x.ExecutingAgency).MaximumLength(ProcedureLimits.Line).WithMessage($"Cơ quan thực hiện không được vượt quá {Line} ký tự.");
        RuleFor(x => x.LevelOfImplementation).MaximumLength(ProcedureLimits.Level).WithMessage("Cấp thực hiện không được vượt quá 255 ký tự.");
        RuleFor(x => x.TargetAudience).MaximumLength(ProcedureLimits.Line).WithMessage($"Đối tượng thực hiện không được vượt quá {Line} ký tự.");
        RuleFor(x => x.FeeSummary).MaximumLength(ProcedureLimits.Line).WithMessage($"Tóm tắt lệ phí không được vượt quá {Line} ký tự.");
        RuleFor(x => x.ProcessingTimeSummary).MaximumLength(ProcedureLimits.Line).WithMessage($"Tóm tắt thời hạn không được vượt quá {Line} ký tự.");
        When(x => x.ContentPayload is not null, () =>
        {
            RuleFor(x => x.ContentPayload!.DecisionNumber).MaximumLength(ProcedureLimits.DecisionNumber).WithMessage("Số quyết định không được vượt quá 255 ký tự.");
            RuleFor(x => x.ContentPayload!).Custom(ValidateContent);
        });
        // BE-H (A.3): trần tổng nội dung. Gắn vào ChecklistSchema để API cập nhật riêng checklist cũng chạy.
        RuleFor(x => x.ChecklistSchema).Custom((_, context) => ValidateSize(context));
        RuleFor(x => x.ChecklistSchema).Custom((items, context) =>
        {
            if (items is null) return;
            if (items.Count > 500) context.AddFailure("ChecklistSchema", "Checklist tối đa 500 mục.");
            var codes = items.Where(x => x is not null).Select(x => x.ChecklistId).ToArray();
            if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
                context.AddFailure("ChecklistSchema", "Mã checklist không được trùng.");
            var cases = context.InstanceToValidate.ContentPayload?.Cases;
            if (cases is not null && items.Any(x => x is not null && !string.IsNullOrWhiteSpace(x.CaseCode)
                && !cases.Any(c => c is not null && c.CaseCode == x.CaseCode)))
                context.AddFailure("ChecklistSchema", "Mã trường hợp của checklist phải thuộc thủ tục.");
            // BE-H (B.3.1): mỗi mã trong formCodes phải là biểu mẫu dùng chung hoặc cùng trường hợp với giấy tờ.
            var forms = context.InstanceToValidate.FormDefinitions ?? [];
            foreach (var item in items.Where(x => x?.FormCodes is { Count: > 0 }))
                foreach (var code in item.FormCodes!.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
                    if (!forms.Any(f => f is not null && f.FormCode == code
                        && (string.IsNullOrWhiteSpace(f.CaseCode) || f.CaseCode == item.CaseCode)))
                        context.AddFailure("ChecklistSchema", $"Giấy tờ '{item.ChecklistId}' gắn biểu mẫu '{code}' không có trong formDefinitions (phải là biểu mẫu dùng chung hoặc cùng trường hợp với giấy tờ).");
        });
        RuleForEach(x => x.ChecklistSchema).Cascade(CascadeMode.Stop).NotNull().WithMessage("Dòng checklist không được để trống.")
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ChecklistId).NotEmpty().WithMessage("Mã checklist không được để trống.")
                    .MaximumLength(255).WithMessage("Mã checklist tối đa 255 ký tự.")
                    .Must(x => x is null || x == x.Trim()).WithMessage("Mã checklist không được có khoảng trắng đầu/cuối.");
                item.RuleFor(x => x.ItemName).NotEmpty().WithMessage("Tên giấy tờ không được để trống.")
                    .MaximumLength(ProcedureLimits.Line).WithMessage($"Tên giấy tờ không được vượt quá {Line} ký tự.");
                item.RuleFor(x => x.SubmissionType).Must(x => x is "NOP" or "XUAT_TRINH").WithMessage("Hình thức giấy tờ phải là NOP hoặc XUAT_TRINH.");
                item.RuleFor(x => x.DocumentCopyType).Must(x => x is null || IsCopyType(x))
                    .WithMessage("Loại bản giấy tờ phải là ORIGINAL, CERTIFIED_COPY, REGULAR_COPY, OTHER hoặc null.");
                item.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng giấy tờ phải lớn hơn 0.");
                item.RuleFor(x => x.QuantityText).MaximumLength(ProcedureLimits.Line).WithMessage($"Mô tả số lượng không được vượt quá {Line} ký tự.");
                // PROC-02a: nhiều loại bản (1 bản chính + 1 bản sao...). Mỗi loại một dòng, không trùng.
                item.RuleFor(x => x.CopyRequirements).Custom((copies, ctx) =>
                {
                    if (copies is null || copies.Count == 0) return;
                    if (copies.Count > 4 || copies.Any(c => c is null || !IsCopyType(c.DocumentCopyType) || c.Quantity < 1 || c.Quantity > 100)
                        || copies.Select(c => c!.DocumentCopyType).Distinct(StringComparer.Ordinal).Count() != copies.Count)
                        ctx.AddFailure("CopyRequirements", "Mỗi loại bản (ORIGINAL, CERTIFIED_COPY, REGULAR_COPY, OTHER) chỉ khai một dòng, số lượng từ 1 đến 100.");
                    // PROC-02b: không âm thầm bỏ thay đổi ở documentCopyType/quantity khi đã có copyRequirements.
                    else if (ctx.InstanceToValidate.ConflictsWithCopyRequirements())
                        ctx.AddFailure("CopyRequirements", "documentCopyType/quantity mâu thuẫn với copyRequirements. Hãy sửa loại bản và số lượng trong copyRequirements và bỏ trống documentCopyType/quantity.");
                });
                item.RuleFor(x => x.FormCodes).Custom((codes, ctx) =>
                {
                    if (codes is null) return;
                    if (codes.Count > ProcedureLimits.FormCodesPerItem || codes.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 255 || c != c.Trim())
                        || codes.Distinct(StringComparer.Ordinal).Count() != codes.Count)
                        ctx.AddFailure("FormCodes", "formCodes tối đa 20 mã, mỗi mã không rỗng, tối đa 255 ký tự, không có khoảng trắng đầu/cuối và không trùng.");
                });
            });
        RuleFor(x => x.FormDefinitions).Custom((forms, context) =>
        {
            if (forms is null) return;
            if (forms.Where(x => x is not null).GroupBy(x => x.FormCode, StringComparer.Ordinal)
                .Any(group => group.Count() > 1 && (group.Any(x => string.IsNullOrWhiteSpace(x.CaseCode))
                    || group.Select(x => x.CaseCode).Distinct(StringComparer.Ordinal).Count() != group.Count())))
                context.AddFailure("FormDefinitions", "Mã biểu mẫu không được trùng trong cùng trường hợp hoặc trùng với biểu mẫu dùng chung.");
            var cases = context.InstanceToValidate.ContentPayload?.Cases;
            if (cases is not null && forms.Any(x => x is not null && !string.IsNullOrWhiteSpace(x.CaseCode)
                && !cases.Any(c => c is not null && c.CaseCode == x.CaseCode)))
                context.AddFailure("FormDefinitions", "Mã trường hợp của biểu mẫu phải thuộc thủ tục.");
        });
        RuleForEach(x => x.FormDefinitions).Cascade(CascadeMode.Stop).NotNull().WithMessage("Biểu mẫu không được để trống.")
            .ChildRules(form =>
            {
                form.RuleFor(x => x.FormCode).NotEmpty().WithMessage("Mã biểu mẫu không được để trống.")
                    .MaximumLength(255).WithMessage("Mã biểu mẫu tối đa 255 ký tự.")
                    .Must(x => x is null || x == x.Trim()).WithMessage("Mã biểu mẫu không được có khoảng trắng đầu/cuối.");
                form.RuleFor(x => x.FormName).NotEmpty().WithMessage("Tên biểu mẫu không được để trống.")
                    .MaximumLength(ProcedureLimits.Line).WithMessage($"Tên biểu mẫu không được vượt quá {Line} ký tự.");
                form.RuleFor(x => x.FormTemplateId).Must(x => x != Guid.Empty)
                    .WithMessage("Mã template phải là GUID hợp lệ hoặc null khi chưa liên kết.");
                form.RuleFor(x => x.FormType).Must(x => x is "ONLINE_INTERACTIVE" or "DOCX_TEMPLATE")
                    .WithMessage("Loại biểu mẫu phải là ONLINE_INTERACTIVE hoặc DOCX_TEMPLATE.");
                form.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng biểu mẫu phải lớn hơn 0.");
            });
    }

    internal static bool IsCopyType(string? value) => value is "ORIGINAL" or "CERTIFIED_COPY" or "REGULAR_COPY" or "OTHER";
    private static bool IsTimeUnit(string? value) => value is "WORKING_DAY" or "DAY" or "HOUR" or "MINUTE";

    private static void ValidateSize(ValidationContext<ProcedureInput> context)
    {
        var x = context.InstanceToValidate;
        var size = JsonSerializer.Serialize(new { x.ContentPayload, x.ChecklistSchema, x.FormDefinitions }, SizeOptions).Length;
        if (size > ProcedureLimits.Payload)
            context.AddFailure("ContentPayload", $"Tổng nội dung thủ tục (trình tự, thành phần hồ sơ, biểu mẫu...) vượt quá 500.000 ký tự (hiện {size:N0}). Hãy rút gọn hoặc tách bớt nội dung.");
    }

    private static void ValidateContent(ProcedureContentPayload payload, ValidationContext<ProcedureInput> context)
    {
        // Danh sách null được coi là rỗng (ProcedureInput.NormalizedContent khi lưu); chỉ kiểm phần tử có gửi lên.
        var cases = payload.Cases ?? [];
        var methods = payload.SubmissionMethods ?? [];
        var legal = payload.LegalReferences ?? [];
        var results = payload.Results ?? [];
        var caseSet = cases.Where(x => x is not null).Select(x => x.CaseCode).ToHashSet(StringComparer.Ordinal);
        bool UnknownCase(string? code) => !string.IsNullOrWhiteSpace(code) && !caseSet.Contains(code);

        // Trường hợp cần mã và tên; danh sách bước được bỏ trống (null coi là rỗng), bước có gửi thì phải hợp lệ.
        if (cases.Any(x => x is null || string.IsNullOrWhiteSpace(x.CaseCode) || string.IsNullOrWhiteSpace(x.CaseName)
            || x.CaseName.Length > ProcedureLimits.Line || !ValidSteps(x.Steps)))
            context.AddFailure("ContentPayload.Cases", $"Trường hợp phải có mã và tên (tối đa {Line} ký tự); bước nếu có phải có tên hoặc nội dung, thứ tự không âm.");
        var caseCodes = cases.Where(x => x is not null).Select(x => x.CaseCode).ToArray();
        if (caseCodes.Any(x => x is not null && (x.Length > 100 || x != x.Trim()))
            || caseCodes.Distinct(StringComparer.Ordinal).Count() != caseCodes.Length)
            context.AddFailure("ContentPayload.Cases", "Mã trường hợp phải duy nhất, tối đa 100 ký tự và không có khoảng trắng đầu/cuối.");
        // BE-H (B.4): bước chung cho thủ tục không chia trường hợp; chỉ cần tên hoặc nội dung bước.
        if (!ValidSteps(payload.CommonSteps))
            context.AddFailure("ContentPayload.CommonSteps", "Bước chung phải có tên hoặc nội dung; thứ tự nếu gửi phải không âm.");

        // BE-H (B.1): phí/thời hạn được null; mọi số có gửi phải không âm.
        if (methods.Any(x => x is null || string.IsNullOrWhiteSpace(x.MethodName) || x.MethodName.Length > ProcedureLimits.Line
            || x.FeeAmount < 0 || x.EstimatedDays < 0 || (x.FeeUnit?.Length ?? 0) > 50 || (x.FeeText?.Length ?? 0) > ProcedureLimits.Line))
            context.AddFailure("ContentPayload.SubmissionMethods", "Cách nộp phải có tên; lệ phí và số ngày nếu có phải không âm; feeUnit tối đa 50 ký tự.");
        if (methods.Any(x => x is not null && UnknownCase(x.CaseCode)))
            context.AddFailure("ContentPayload.SubmissionMethods", "Mã trường hợp của cách nộp phải thuộc thủ tục.");
        if (methods.Any(x => x?.Fees is { } fees && (fees.Count > ProcedureLimits.FeesPerMethod || fees.Any(f => f is null
            || f.Amount < 0 || f.Percent is < 0 or > 100 || f.Kind is not (null or "PHI" or "LE_PHI")
            || (f.Unit?.Length ?? 0) > 50 || (f.Description?.Length ?? 0) > ProcedureLimits.Line || UnknownCase(f.CaseCode)))))
            context.AddFailure("ContentPayload.SubmissionMethods.Fees", "Mức phí: tối đa 100 mức/cách nộp; amount không âm; percent 0–100; kind là PHI, LE_PHI hoặc null; unit tối đa 50 ký tự; caseCode phải thuộc thủ tục.");
        if (methods.Any(x => x?.ProcessingTime is { } t && (t.Value < 0 || (t.Unit is not null && !IsTimeUnit(t.Unit))
            || (t.Text?.Length ?? 0) > ProcedureLimits.Line)))
            context.AddFailure("ContentPayload.SubmissionMethods.ProcessingTime", "Thời hạn: value không âm; unit là WORKING_DAY, DAY, HOUR, MINUTE hoặc null.");

        if (legal.Any(x => x is null) || results.Any(string.IsNullOrWhiteSpace))
            context.AddFailure("ContentPayload", "Căn cứ pháp lý và kết quả không được chứa phần tử trống.");

        // BE-H (B.2): danh sách văn bản tuỳ chọn.
        ValidateList(payload.Levels, "ContentPayload.Levels", context);
        ValidateList(payload.CompetentAuthorities, "ContentPayload.CompetentAuthorities", context);
        ValidateList(payload.ExecutingAgencies, "ContentPayload.ExecutingAgencies", context);
        ValidateList(payload.AuthorizedAgencies, "ContentPayload.AuthorizedAgencies", context);
        ValidateList(payload.CoordinatingAgencies, "ContentPayload.CoordinatingAgencies", context);
        ValidateList(payload.ReceivingAddresses, "ContentPayload.ReceivingAddresses", context);
        ValidateList(payload.Keywords, "ContentPayload.Keywords", context);
        if ((payload.ProcedureType?.Length ?? 0) > ProcedureLimits.Line)
            context.AddFailure("ContentPayload.ProcedureType", $"Loại thủ tục không được vượt quá {Line} ký tự.");
        if (payload.ResultDetails is { } details && (details.Count > ProcedureLimits.ListItems || details.Any(d => d is null
            || string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > ProcedureLimits.Line || (d.Code?.Length ?? 0) > 255
            || (d.Validity?.Length ?? 0) > ProcedureLimits.Line)))
            context.AddFailure("ContentPayload.ResultDetails", $"Kết quả: tối đa 100 dòng, mỗi dòng có tên (tối đa {Line} ký tự), mã tối đa 255 ký tự.");
        // BE-H (Phần C): mục mở rộng.
        if (payload.AdditionalSections is { } sections && (sections.Count > ProcedureLimits.Sections || sections.Any(s => s is null
            || string.IsNullOrWhiteSpace(s.Title) || s.Title.Length > ProcedureLimits.SectionTitle)))
            context.AddFailure("ContentPayload.AdditionalSections", "Mục mở rộng: tối đa 50 mục, mỗi mục có tiêu đề không rỗng, tối đa 255 ký tự.");
    }

    private static bool ValidSteps(List<ProcedureStepDetail>? steps) =>
        (steps ?? []).All(step => step is not null && step.StepOrder >= 0
            && (!string.IsNullOrWhiteSpace(step.StepName) || !string.IsNullOrWhiteSpace(step.ActionDetails)));

    private static void ValidateList(List<string>? values, string name, ValidationContext<ProcedureInput> context)
    {
        if (values is null) return;
        if (values.Count > ProcedureLimits.ListItems || values.Any(v => (v?.Length ?? 0) > ProcedureLimits.Line))
            context.AddFailure(name, $"Danh sách tối đa 100 phần tử, mỗi phần tử tối đa {Line} ký tự.");
    }
}
