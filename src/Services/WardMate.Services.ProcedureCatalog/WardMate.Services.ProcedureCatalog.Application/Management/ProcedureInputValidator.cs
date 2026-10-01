using FluentValidation;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed class ProcedureInputValidator : AbstractValidator<ProcedureInput>
{
    public ProcedureInputValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Danh mục phải là số nguyên lớn hơn 0.");
        RuleFor(x => x.ProcedureCode).NotEmpty().WithMessage("Mã thủ tục không được để trống.")
            .MaximumLength(50).WithMessage("Mã thủ tục không được vượt quá 50 ký tự.");
        RuleFor(x => x.Title).Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length >= 10)
            .WithMessage("Tên thủ tục phải có ít nhất 10 ký tự.")
            .MaximumLength(500).WithMessage("Tên thủ tục không được vượt quá 500 ký tự.");
        RuleFor(x => x.IssuingAuthority).MaximumLength(255).WithMessage("Cơ quan ban hành không được vượt quá 255 ký tự.");
        RuleFor(x => x.LevelOfImplementation).NotEmpty().WithMessage("Cấp thực hiện không được để trống.")
            .MaximumLength(50).WithMessage("Cấp thực hiện không được vượt quá 50 ký tự.");
        RuleFor(x => x.TargetAudience).NotEmpty().WithMessage("Đối tượng thực hiện không được để trống.")
            .MaximumLength(255).WithMessage("Đối tượng thực hiện không được vượt quá 255 ký tự.");
        RuleFor(x => x.FeeSummary).NotEmpty().WithMessage("Tóm tắt lệ phí không được để trống.")
            .MaximumLength(255).WithMessage("Tóm tắt lệ phí không được vượt quá 255 ký tự.");
        RuleFor(x => x.ProcessingTimeSummary).NotEmpty().WithMessage("Thời gian xử lý không được để trống.")
            .MaximumLength(255).WithMessage("Thời gian xử lý không được vượt quá 255 ký tự.");
        RuleFor(x => x.ContentPayload).NotNull().WithMessage("Nội dung thủ tục không được để trống.");
        When(x => x.ContentPayload is not null, () =>
        {
            RuleFor(x => x.ContentPayload.DecisionNumber).MaximumLength(100).WithMessage("Số quyết định không được vượt quá 100 ký tự.");
            RuleFor(x => x.ContentPayload).Custom(ValidateContent);
        });
        RuleForEach(x => x.ChecklistSchema).NotNull().WithMessage("Dòng checklist không được để trống.")
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ChecklistId).NotEmpty().WithMessage("Mã checklist không được để trống.");
                item.RuleFor(x => x.ItemName).NotEmpty().WithMessage("Tên giấy tờ không được để trống.")
                    .MaximumLength(255).WithMessage("Tên giấy tờ không được vượt quá 255 ký tự.");
                item.RuleFor(x => x.SubmissionType).Must(x => x is "NOP" or "XUAT_TRINH").WithMessage("Hình thức giấy tờ phải là NOP hoặc XUAT_TRINH.");
                item.RuleFor(x => x.DocumentCopyType).Must(x => x is "ORIGINAL" or "CERTIFIED_COPY" or "REGULAR_COPY")
                    .WithMessage("Loại bản giấy tờ phải là ORIGINAL, CERTIFIED_COPY hoặc REGULAR_COPY.");
                item.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng giấy tờ phải lớn hơn 0.");
            });
        RuleForEach(x => x.FormDefinitions).NotNull().WithMessage("Biểu mẫu không được để trống.")
            .ChildRules(form =>
            {
                form.RuleFor(x => x.FormCode).NotEmpty().WithMessage("Mã biểu mẫu không được để trống.");
                form.RuleFor(x => x.FormName).NotEmpty().WithMessage("Tên biểu mẫu không được để trống.");
                form.RuleFor(x => x.FormType).Must(x => x is "ONLINE_INTERACTIVE" or "DOCX_TEMPLATE")
                    .WithMessage("Loại biểu mẫu phải là ONLINE_INTERACTIVE hoặc DOCX_TEMPLATE.");
                form.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng biểu mẫu phải lớn hơn 0.");
            });
    }

    private static void ValidateContent(ProcedureContentPayload payload, ValidationContext<ProcedureInput> context)
    {
        if (payload.Cases is null || payload.SubmissionMethods is null || payload.LegalReferences is null || payload.Results is null)
        {
            context.AddFailure("ContentPayload", "Các danh sách trong nội dung thủ tục không được là null; hãy gửi mảng rỗng khi không có dữ liệu.");
            return;
        }
        if (payload.Cases.Any(x => x is null || string.IsNullOrWhiteSpace(x.CaseCode) ||
            string.IsNullOrWhiteSpace(x.CaseName) || x.Steps is null ||
            x.Steps.Any(step => step is null || step.StepOrder <= 0 || string.IsNullOrWhiteSpace(step.StepName))))
            context.AddFailure("ContentPayload.Cases", "Trường hợp phải có mã, tên và danh sách bước hợp lệ (thứ tự lớn hơn 0, tên không trống).");
        if (payload.SubmissionMethods.Any(x => x is null || string.IsNullOrWhiteSpace(x.MethodName) || x.FeeAmount < 0 || x.EstimatedDays < 0))
            context.AddFailure("ContentPayload.SubmissionMethods", "Phương thức nộp phải có tên, lệ phí và thời gian không âm.");
        if (payload.LegalReferences.Any(x => x is null) || payload.Results.Any(string.IsNullOrWhiteSpace))
            context.AddFailure("ContentPayload", "Căn cứ pháp lý và kết quả không được chứa phần tử trống.");
    }
}
