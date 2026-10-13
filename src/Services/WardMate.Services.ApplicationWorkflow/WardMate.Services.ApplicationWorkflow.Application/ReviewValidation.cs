using System.Text;
using System.Text.Json;
using FluentValidation;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed class AssignOfficerValidator : AbstractValidator<AssignOfficerCommand>
{
    public AssignOfficerValidator() { RuleFor(x => x.Id).NotEmpty().WithMessage("Mã hồ sơ không hợp lệ."); }
}
public sealed class AddApplicationCommentValidator : AbstractValidator<AddApplicationCommentCommand>
{
    public AddApplicationCommentValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã hồ sơ không hợp lệ.");
        RuleFor(x => x.Input).NotNull().WithMessage("Nhận xét không được để trống.");
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.VersionNumber).GreaterThan(0).WithMessage("Phiên bản phải lớn hơn 0.");
            RuleFor(x => x.Input.TargetType).Must(x => x is "FORM_FIELD" or "CHECKLIST_ITEM").WithMessage("Loại mục phải là FORM_FIELD hoặc CHECKLIST_ITEM.");
            RuleFor(x => x.Input.TargetId).NotEmpty().WithMessage("Mã mục không được trống.").MaximumLength(100).WithMessage("Mã mục tối đa 100 ký tự.");
            RuleFor(x => x.Input.FieldLabel).NotEmpty().WithMessage("Tên hiển thị không được trống.").MaximumLength(255).WithMessage("Tên hiển thị tối đa 255 ký tự.");
            RuleFor(x => x.Input.CommentText).NotEmpty().WithMessage("Nội dung nhận xét không được trống.").MaximumLength(4000).WithMessage("Nhận xét tối đa 4000 ký tự.");
        });
    }
}
public sealed class RequestRevisionValidator : AbstractValidator<RequestRevisionCommand>
{
    public RequestRevisionValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã hồ sơ không hợp lệ.");
        RuleFor(x => x.Input).NotNull().WithMessage("Cần nhập lý do yêu cầu sửa.");
        When(x => x.Input is not null, () => RuleFor(x => x.Input.Reason).NotEmpty().WithMessage("Lý do không được trống.")
            .MaximumLength(4000).WithMessage("Lý do tối đa 4000 ký tự."));
    }
}
public sealed class ResubmitValidator : AbstractValidator<ResubmitApplicationCommand>
{
    public ResubmitValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã hồ sơ không hợp lệ.");
        RuleFor(x => x.Input).NotNull().WithMessage("Dữ liệu nộp lại không được trống.");
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.ExpectedVersionNumber).GreaterThan(0).WithMessage("Phiên bản phải lớn hơn 0.");
            RuleFor(x => x.Input.FormData).Must(x => x.ValueKind == JsonValueKind.Object && Encoding.UTF8.GetByteCount(x.GetRawText()) <= 65536)
                .WithMessage("Dữ liệu form phải là đối tượng JSON và tối đa 64 KiB.");
            RuleFor(x => x.Input.Checklists).NotNull().WithMessage("Cần gửi danh sách checklist (có thể rỗng).");
            When(x => x.Input.Checklists is not null, () =>
            {
                RuleFor(x => x.Input.Checklists).Must(x => x.All(i => i is not null) && x.Select(i => i.Id).Distinct().Count() == x.Length)
                    .WithMessage("Checklist không được có mục null hoặc trùng mã.");
                RuleForEach(x => x.Input.Checklists).SetValidator(new ResubmitChecklistValidator());
            });
        });
    }
}
public sealed class ResubmitChecklistValidator : AbstractValidator<ResubmitChecklistInput>
{
    public ResubmitChecklistValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã checklist không hợp lệ.");
        RuleFor(x => x.Status).Must(x => x is "PENDING" or "COMPLETED").WithMessage("Trạng thái phải là PENDING hoặc COMPLETED.");
        RuleFor(x => x.FileUrl).MaximumLength(500).WithMessage("URL tệp tối đa 500 ký tự.")
            .Must(x => x is null || Uri.TryCreate(x, UriKind.Absolute, out var u) && u.Scheme == "https" && string.IsNullOrEmpty(u.UserInfo))
            .WithMessage("URL tệp phải là HTTPS hợp lệ, không chứa thông tin đăng nhập.");
        RuleFor(x => x.Note).MaximumLength(4000).WithMessage("Ghi chú tối đa 4000 ký tự.");
    }
}
public sealed class OfficerPendingValidator : AbstractValidator<GetOfficerPendingApplicationsPagedQuery>
{
    public OfficerPendingValidator()
    {
        RuleFor(x => x.Input.Page).InclusiveBetween(1, 1000000).WithMessage("Trang phải từ 1 đến 1000000.");
        RuleFor(x => x.Input.PageSize).InclusiveBetween(1, 100).WithMessage("Số hồ sơ mỗi trang phải từ 1 đến 100.");
        RuleFor(x => x.Input.Status).Must(x => x is null or "SUBMITTED" or "UNDER_REVIEW").WithMessage("Trạng thái phải là SUBMITTED hoặc UNDER_REVIEW.");
        RuleFor(x => x.Input).Must(x => x.SubmittedFrom is null || x.SubmittedTo is null || x.SubmittedFrom <= x.SubmittedTo)
            .WithMessage("Ngày bắt đầu không được sau ngày kết thúc.");
    }
}
public sealed class CommentsQueryValidator : AbstractValidator<GetApplicationCommentsQuery>
{
    public CommentsQueryValidator()
    {
        RuleFor(x => x.VersionNumber).Must(x => x is null or > 0).WithMessage("Phiên bản phải lớn hơn 0.");
        RuleFor(x => x.Status).Must(x => x is null or "OPEN" or "RESOLVED").WithMessage("Trạng thái nhận xét phải là OPEN hoặc RESOLVED.");
    }
}
public sealed class DiffQueryValidator : AbstractValidator<CompareApplicationVersionsQuery>
{
    public DiffQueryValidator()
    {
        RuleFor(x => x.FromVersionNumber).GreaterThan(0).WithMessage("Phiên bản nguồn phải lớn hơn 0.");
        RuleFor(x => x.ToVersionNumber).GreaterThan(x => x.FromVersionNumber).WithMessage("Phiên bản đích phải lớn hơn phiên bản nguồn.");
    }
}
