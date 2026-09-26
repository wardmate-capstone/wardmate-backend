using FluentValidation;

namespace WardMate.Services.IAM.Application.Rbac;

public sealed class RoleInputValidator : AbstractValidator<RoleInput>
{
    public RoleInputValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty().WithMessage("Tên vai trò không được để trống.")
            .MaximumLength(50).WithMessage("Tên vai trò không được vượt quá 50 ký tự.")
            .Matches("^[A-Za-z][A-Za-z0-9_]*$").WithMessage("Tên vai trò phải bắt đầu bằng chữ cái không dấu và chỉ chứa chữ cái, chữ số hoặc dấu gạch dưới.");
        RuleFor(x => x.Description).MaximumLength(2000).WithMessage("Mô tả vai trò không được vượt quá 2000 ký tự.");
    }
}
public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator(RoleInputValidator input)
    {
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("Mã người thực hiện không hợp lệ.");
        RuleFor(x => x.Input).NotNull().WithMessage("Thông tin vai trò không được để trống.").SetValidator(input);
    }
}
public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator(RoleInputValidator input)
    {
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("Mã người thực hiện không hợp lệ.");
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Mã vai trò phải lớn hơn 0.");
        RuleFor(x => x.Input).NotNull().WithMessage("Thông tin vai trò không được để trống.").SetValidator(input);
    }
}
public sealed class DeleteRoleValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("Mã người thực hiện không hợp lệ.");
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Mã vai trò phải lớn hơn 0.");
    }
}
public sealed class SetRolePermissionValidator : AbstractValidator<SetRolePermissionCommand>
{
    public SetRolePermissionValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("Mã người thực hiện không hợp lệ.");
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Mã vai trò phải lớn hơn 0.");
        RuleFor(x => x.PermissionId).GreaterThan(0).WithMessage("Mã quyền phải lớn hơn 0.");
    }
}
public sealed class SetUserRoleValidator : AbstractValidator<SetUserRoleCommand>
{
    public SetUserRoleValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty().WithMessage("Mã người thực hiện không hợp lệ.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Mã vai trò phải lớn hơn 0.");
    }
}
public sealed class ListRolesValidator : AbstractValidator<ListRolesQuery>
{
    public ListRolesValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000).WithMessage("Số trang phải từ 1 đến 1000000.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số vai trò mỗi trang phải từ 1 đến 100.");
    }
}
public sealed class ListRbacAuditValidator : AbstractValidator<ListRbacAuditQuery>
{
    public ListRbacAuditValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000).WithMessage("Số trang phải từ 1 đến 1000000.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số bản ghi mỗi trang phải từ 1 đến 100.");
    }
}
