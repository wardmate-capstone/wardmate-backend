using FluentValidation;

namespace WardMate.Services.IAM.Application.Profiles;

public sealed class ListProfilesValidator : AbstractValidator<ListProfilesQuery>
{
    public ListProfilesValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000).WithMessage("Số trang phải từ 1 đến 1000000.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số hồ sơ mỗi trang phải từ 1 đến 100.");
    }
}

public sealed class ProfileInputValidator : AbstractValidator<ProfileInput>
{
    public ProfileInputValidator(TimeProvider clock)
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(255).WithMessage("Họ và tên không được vượt quá 255 ký tự.");
        RuleFor(x => x.IdentityNumber).MaximumLength(20).WithMessage("Số giấy tờ định danh không được vượt quá 20 ký tự.");
        RuleFor(x => x.PhoneNumber).MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự.")
            .Matches(@"^\+?[0-9 ()-]+$").When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Số điện thoại không đúng định dạng.");
        RuleFor(x => x.Gender).MaximumLength(10).WithMessage("Giới tính không được vượt quá 10 ký tự.");
        RuleFor(x => x.DateOfBirth).Must(date => date is null || date <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7)))
            .WithMessage("Ngày sinh không được nằm trong tương lai.");
        RuleFor(x => x.PermanentAddress).MaximumLength(4000).WithMessage("Địa chỉ thường trú không được vượt quá 4000 ký tự.");
        RuleFor(x => x.TemporaryAddress).MaximumLength(4000).WithMessage("Địa chỉ tạm trú không được vượt quá 4000 ký tự.");
    }
}
public sealed class CreateProfileValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileValidator(ProfileInputValidator profile)
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
        RuleFor(x => x.Profile).NotNull().WithMessage("Thông tin hồ sơ không được để trống.").SetValidator(profile);
    }
}
public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator(ProfileInputValidator profile)
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
        RuleFor(x => x.Profile).NotNull().WithMessage("Thông tin hồ sơ không được để trống.").SetValidator(profile);
    }
}
