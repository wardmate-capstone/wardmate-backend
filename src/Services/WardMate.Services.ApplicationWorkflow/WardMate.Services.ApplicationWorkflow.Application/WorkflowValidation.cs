using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed class CreateApplicationValidator : AbstractValidator<CreateApplicationCommand>
{
    public CreateApplicationValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Người nộp không hợp lệ.");
        RuleFor(x => x.Input).NotNull().WithMessage("Thông tin hồ sơ không được để trống.");
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.ProcedureId).NotEmpty().WithMessage("Mã thủ tục không được để trống.");
            RuleFor(x => x.Input.CaseCode).MaximumLength(100).WithMessage("Mã trường hợp tối đa 100 ký tự.");
            RuleFor(x => x.Input.FormData).Must(x => x.ValueKind == JsonValueKind.Object
                && Encoding.UTF8.GetByteCount(x.GetRawText()) <= 65536)
                .WithMessage("Dữ liệu form phải là đối tượng JSON và tối đa 64 KiB.");
        });
    }
}
public sealed class UpdateChecklistValidator : AbstractValidator<UpdateChecklistCommand>
{
    public UpdateChecklistValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Mã hồ sơ không hợp lệ.");
        RuleFor(x => x.ChecklistId).NotEmpty().WithMessage("Mã mục checklist không hợp lệ.");
        RuleFor(x => x.Input).NotNull().WithMessage("Thông tin checklist không được để trống.");
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.Status).Must(x => x is ChecklistStates.Pending or ChecklistStates.Completed or ChecklistStates.Rejected)
                .WithMessage("Trạng thái phải là PENDING, COMPLETED hoặc REJECTED.");
            RuleFor(x => x.Input.FileUrl).MaximumLength(500).WithMessage("URL tệp tối đa 500 ký tự.")
                .Must(x => string.IsNullOrWhiteSpace(x) || (Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https"
                    && string.IsNullOrEmpty(uri.UserInfo)))
                .WithMessage("URL tệp phải là HTTPS hợp lệ, không chứa thông tin đăng nhập.");
            RuleFor(x => x.Input.Note).MaximumLength(4000).WithMessage("Ghi chú tối đa 4000 ký tự.");
        });
    }
}
public sealed class ListApplicationsValidator : AbstractValidator<ListApplicationsQuery>
{
    public ListApplicationsValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000).WithMessage("Trang phải từ 1 đến 1000000.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số hồ sơ mỗi trang phải từ 1 đến 100.");
    }
}
public sealed class WorkflowValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = (await Task.WhenAll(validators.Select(x => x.ValidateAsync(request, ct))))
            .SelectMany(x => x.Errors).ToArray();
        if (failures.Length > 0) throw new ValidationException(failures);
        return await next();
    }
}
public static class WorkflowApplicationRegistration
{
    public static IServiceCollection AddWorkflowApplication(this IServiceCollection services)
    {
        services.AddMediatR(x => x.RegisterServicesFromAssembly(typeof(WorkflowHandlers).Assembly));
        services.AddValidatorsFromAssembly(typeof(WorkflowHandlers).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(WorkflowValidationBehavior<,>));
        return services;
    }
}
