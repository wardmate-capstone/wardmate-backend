using FluentValidation;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed class ProcedureInputValidator : AbstractValidator<ProcedureInput>
{
    public ProcedureInputValidator()
    {
        RuleFor(x => x.OriginalPdfUrl).MaximumLength(500).WithMessage("URL PDF không được vượt quá 500 ký tự.")
            .Must(x => x is null || (Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)))
            .WithMessage("URL PDF phải là đường dẫn HTTPS tuyệt đối hoặc null.");
        RuleFor(x => x.PdfFileName).MaximumLength(255).WithMessage("Tên file PDF không được vượt quá 255 ký tự.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Danh mục phải là số nguyên lớn hơn 0.");
        RuleFor(x => x.ProcedureCode).NotEmpty().WithMessage("Mã thủ tục không được để trống.")
            .MaximumLength(50).WithMessage("Mã thủ tục không được vượt quá 50 ký tự.");
        // PROC-01b: chỉ mã, tên, lĩnh vực bắt buộc; các trường còn lại chỉ kiểm độ dài khi có giá trị.
        RuleFor(x => x.Title).Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Tên thủ tục không được để trống.")
            .MaximumLength(500).WithMessage("Tên thủ tục không được vượt quá 500 ký tự.");
        RuleFor(x => x.IssuingAuthority).MaximumLength(255).WithMessage("Cơ quan ban hành không được vượt quá 255 ký tự.");
        RuleFor(x => x.LevelOfImplementation).MaximumLength(50).WithMessage("Cấp thực hiện không được vượt quá 50 ký tự.");
        RuleFor(x => x.TargetAudience).MaximumLength(255).WithMessage("Đối tượng thực hiện không được vượt quá 255 ký tự.");
        RuleFor(x => x.FeeSummary).MaximumLength(255).WithMessage("Tóm tắt lệ phí không được vượt quá 255 ký tự.");
        RuleFor(x => x.ProcessingTimeSummary).MaximumLength(255).WithMessage("Thời gian xử lý không được vượt quá 255 ký tự.");
        When(x => x.ContentPayload is not null, () =>
        {
            RuleFor(x => x.ContentPayload!.DecisionNumber).MaximumLength(100).WithMessage("Số quyết định không được vượt quá 100 ký tự.");
            RuleFor(x => x.ContentPayload!).Custom(ValidateContent);
        });
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
        });
        RuleForEach(x => x.ChecklistSchema).Cascade(CascadeMode.Stop).NotNull().WithMessage("Dòng checklist không được để trống.")
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ChecklistId).NotEmpty().WithMessage("Mã checklist không được để trống.")
                    .MaximumLength(255).WithMessage("Mã checklist tối đa 255 ký tự.")
                    .Must(x => x is null || x == x.Trim()).WithMessage("Mã checklist không được có khoảng trắng đầu/cuối.");
                item.RuleFor(x => x.ItemName).NotEmpty().WithMessage("Tên giấy tờ không được để trống.")
                    .MaximumLength(255).WithMessage("Tên giấy tờ không được vượt quá 255 ký tự.");
                item.RuleFor(x => x.SubmissionType).Must(x => x is "NOP" or "XUAT_TRINH").WithMessage("Hình thức giấy tờ phải là NOP hoặc XUAT_TRINH.");
                item.RuleFor(x => x.DocumentCopyType).Must(x => x is null || IsCopyType(x))
                    .WithMessage("Loại bản giấy tờ phải là ORIGINAL, CERTIFIED_COPY, REGULAR_COPY hoặc null.");
                item.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng giấy tờ phải lớn hơn 0.");
                // PROC-02a: nhiều loại bản (1 bản chính + 1 bản sao...). Mỗi loại một dòng, không trùng.
                item.RuleFor(x => x.CopyRequirements).Custom((copies, ctx) =>
                {
                    if (copies is null || copies.Count == 0) return;
                    if (copies.Count > 3 || copies.Any(c => c is null || !IsCopyType(c.DocumentCopyType) || c.Quantity < 1 || c.Quantity > 100)
                        || copies.Select(c => c!.DocumentCopyType).Distinct(StringComparer.Ordinal).Count() != copies.Count)
                        ctx.AddFailure("CopyRequirements", "Mỗi loại bản (ORIGINAL, CERTIFIED_COPY, REGULAR_COPY) chỉ khai một dòng, số lượng từ 1 đến 100.");
                    // PROC-02b: không âm thầm bỏ thay đổi ở documentCopyType/quantity khi đã có copyRequirements.
                    else if (ctx.InstanceToValidate.ConflictsWithCopyRequirements())
                        ctx.AddFailure("CopyRequirements", "documentCopyType/quantity mâu thuẫn với copyRequirements. Hãy sửa loại bản và số lượng trong copyRequirements và bỏ trống documentCopyType/quantity.");
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
                    .MaximumLength(255).WithMessage("Tên biểu mẫu tối đa 255 ký tự.");
                form.RuleFor(x => x.FormTemplateId).Must(x => x != Guid.Empty)
                    .WithMessage("Mã template phải là GUID hợp lệ hoặc null khi chưa liên kết.");
                form.RuleFor(x => x.FormType).Must(x => x is "ONLINE_INTERACTIVE" or "DOCX_TEMPLATE")
                    .WithMessage("Loại biểu mẫu phải là ONLINE_INTERACTIVE hoặc DOCX_TEMPLATE.");
                form.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng biểu mẫu phải lớn hơn 0.");
            });
    }

    private static bool IsCopyType(string? value) => value is "ORIGINAL" or "CERTIFIED_COPY" or "REGULAR_COPY";

    private static void ValidateContent(ProcedureContentPayload payload, ValidationContext<ProcedureInput> context)
    {
        // Danh sách null được coi là rỗng (ProcedureInput.NormalizedContent khi lưu); chỉ kiểm phần tử có gửi lên.
        var cases = payload.Cases ?? [];
        var methods = payload.SubmissionMethods ?? [];
        var legal = payload.LegalReferences ?? [];
        var results = payload.Results ?? [];
        // Trường hợp cần mã và tên; danh sách bước được bỏ trống (null coi là rỗng), bước có gửi thì phải hợp lệ.
        if (cases.Any(x => x is null || string.IsNullOrWhiteSpace(x.CaseCode) || string.IsNullOrWhiteSpace(x.CaseName)
            || (x.CaseName?.Length ?? 0) > 500
            || (x.Steps ?? []).Any(step => step is null || step.StepOrder <= 0 || string.IsNullOrWhiteSpace(step.StepName))))
            context.AddFailure("ContentPayload.Cases", "Trường hợp phải có mã và tên (tối đa 500 ký tự); bước nếu có phải có thứ tự lớn hơn 0 và tên không trống.");
        var caseCodes = cases.Where(x => x is not null).Select(x => x.CaseCode).ToArray();
        if (caseCodes.Any(x => x is not null && (x.Length > 100 || x != x.Trim()))
            || caseCodes.Distinct(StringComparer.Ordinal).Count() != caseCodes.Length)
            context.AddFailure("ContentPayload.Cases", "Mã trường hợp phải duy nhất, tối đa 100 ký tự và không có khoảng trắng đầu/cuối.");
        if (methods.Any(x => x is null || string.IsNullOrWhiteSpace(x.MethodName) || x.FeeAmount < 0 || x.EstimatedDays < 0))
            context.AddFailure("ContentPayload.SubmissionMethods", "Phương thức nộp phải có tên, lệ phí và thời gian không âm.");
        if (legal.Any(x => x is null) || results.Any(string.IsNullOrWhiteSpace))
            context.AddFailure("ContentPayload", "Căn cứ pháp lý và kết quả không được chứa phần tử trống.");
    }
}
