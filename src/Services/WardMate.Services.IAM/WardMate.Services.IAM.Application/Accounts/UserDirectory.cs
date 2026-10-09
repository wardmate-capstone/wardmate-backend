using FluentValidation;
using MediatR;
using WardMate.Services.IAM.Application.Common;

namespace WardMate.Services.IAM.Application.Accounts;

public sealed record DirectoryFilter(string? Search = null, string? Role = null, string? WardCode = null, int? AssignedCategory = null, int Page = 1, int PageSize = 20);
public sealed record DirectoryUser(Guid Id, string Username, string Email, string? FullName, string? IdentityNumber,
    bool IsActive, string? WardCode, string[] Roles, int[] AssignedCategories);
public sealed record DirectoryPage(DirectoryUser[] Items, int Page, int PageSize, int Total);
public sealed record DirectoryQuery(Guid ActorId, bool Admin, DirectoryFilter Filter) : IRequest<Result<DirectoryPage>>;
public sealed record AssignCategoriesCommand(Guid ActorId, Guid UserId, int[] Categories) : IRequest<Result<bool>>;
public sealed record AccessContextDto(Guid UserId, string? WardCode, string[] Roles, string[] Permissions);
public interface IUserDirectory
{
    Task<Result<DirectoryPage>> List(DirectoryQuery query, CancellationToken ct);
    Task<Result<bool>> Assign(AssignCategoriesCommand command, CancellationToken ct);
    Task<AccessContextDto?> Access(Guid userId, CancellationToken ct);
    Task<WardDto[]> Wards(CancellationToken ct);
}
public sealed class DirectoryHandlers(IUserDirectory store) : IRequestHandler<DirectoryQuery, Result<DirectoryPage>>, IRequestHandler<AssignCategoriesCommand, Result<bool>>
{
    public Task<Result<DirectoryPage>> Handle(DirectoryQuery r, CancellationToken ct) => store.List(r, ct);
    public Task<Result<bool>> Handle(AssignCategoriesCommand r, CancellationToken ct) => store.Assign(r, ct);
}
public sealed class DirectoryValidator : AbstractValidator<DirectoryQuery>
{
    public DirectoryValidator()
    {
        RuleFor(x => x.Filter.Page).InclusiveBetween(1, 1000000).WithMessage("Trang phải từ 1 đến 1000000.");
        RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100).WithMessage("Số bản ghi phải từ 1 đến 100.");
        RuleFor(x => x.Filter.Search).MaximumLength(255).WithMessage("Từ khóa tối đa 255 ký tự.");
        RuleFor(x => x.Filter.Role).MaximumLength(50).WithMessage("Vai trò tối đa 50 ký tự.");
        RuleFor(x => x.Filter.WardCode).MaximumLength(50).WithMessage("Mã phường tối đa 50 ký tự.");
        RuleFor(x => x.Filter.AssignedCategory).Must(x => x is null or > 0).WithMessage("Mã danh mục phải lớn hơn 0.");
    }
}
public sealed class AssignCategoriesValidator : AbstractValidator<AssignCategoriesCommand>
{
    public AssignCategoriesValidator()
    {
        RuleFor(x => x.Categories).NotNull().WithMessage("Danh mục không được null.")
            .Must(x => x is null || x.Length <= 100 && x.All(id => id > 0) && x.Distinct().Count() == x.Length)
            .WithMessage("Tối đa 100 danh mục, mã dương và không trùng.");
    }
}
