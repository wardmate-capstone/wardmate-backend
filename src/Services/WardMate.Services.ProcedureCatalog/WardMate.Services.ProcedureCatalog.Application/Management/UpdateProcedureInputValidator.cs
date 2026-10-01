using FluentValidation;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed class UpdateProcedureInputValidator : AbstractValidator<UpdateProcedureInput>
{
    public UpdateProcedureInputValidator(IValidator<ProcedureInput> inputValidator)
    {
        Include(inputValidator);
        RuleFor(x => x.DecisionNumber).NotEmpty().WithMessage("Số quyết định không được để trống.")
            .MaximumLength(100).WithMessage("Số quyết định không được vượt quá 100 ký tự.");
        RuleFor(x => x.EffectiveDate).NotEmpty().WithMessage("Ngày có hiệu lực không được để trống.");
    }
}
