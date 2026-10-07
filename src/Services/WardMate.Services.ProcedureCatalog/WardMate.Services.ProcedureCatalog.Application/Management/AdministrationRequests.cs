using FluentValidation;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;

namespace WardMate.Services.ProcedureCatalog.Application.Management;

public sealed record CategoryInput(string CategoryName, string? Description);
public sealed record RollbackInput(string Reason, string DecisionNumber, DateOnly EffectiveDate);
public sealed record SourceLinkDto(string Url, int ExpiresInSeconds = 600);
public sealed record DocumentFormOptionDto(Guid Id, string FormCode, string FormName, string FormType);
public sealed record ManagerDetailQuery(Guid Id) : IRequest<ProcedureResult<ProcedureDetailDto>>;
public sealed record SaveCategoryCommand(int? Id, CategoryInput Input) : IRequest<ProcedureResult<ProcedureCategoryDto>>;
public sealed record DeleteCategoryCommand(int Id) : IRequest<ProcedureResult<bool>>;
public sealed record DiscardDraftCommand(Guid Id) : IRequest<ProcedureResult<bool>>;
public sealed record RollbackProcedureCommand(Guid Id, int VersionNumber, RollbackInput Input, string Actor)
    : IRequest<ProcedureResult<ProcedureDetailDto>>;
public sealed record VersionSourceQuery(Guid Id, Guid VersionId) : IRequest<ProcedureResult<SourceLinkDto>>;
public sealed record DocumentFormsQuery(int Page = 1, int PageSize = 50, string? SearchCode = null)
    : IRequest<ProcedureResult<DocumentFormOptionDto[]>>;

public interface IProcedureAdministration
{
    Task<ProcedureResult<ProcedureDetailDto>> Detail(Guid id, CancellationToken ct);
    Task<ProcedureResult<ProcedureCategoryDto>> SaveCategory(int? id, CategoryInput input, CancellationToken ct);
    Task<ProcedureResult<bool>> DeleteCategory(int id, CancellationToken ct);
    Task<ProcedureResult<bool>> DiscardDraft(Guid id, CancellationToken ct);
    Task<ProcedureResult<ProcedureDetailDto>> Rollback(RollbackProcedureCommand command, CancellationToken ct);
    Task<ProcedureResult<SourceLinkDto>> VersionSource(Guid id, Guid versionId, CancellationToken ct);
}
public interface IDocumentFormsClient
{
    Task<ProcedureResult<DocumentFormOptionDto[]>> List(DocumentFormsQuery query, CancellationToken ct);
}

public sealed class CategoryInputValidator : AbstractValidator<CategoryInput>
{
    public CategoryInputValidator()
    {
        RuleFor(x => x.CategoryName).NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(255).WithMessage("Tên danh mục tối đa 255 ký tự.");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("Mô tả tối đa 4000 ký tự.");
    }
}
public sealed class RollbackInputValidator : AbstractValidator<RollbackInput>
{
    public RollbackInputValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Cần nhập lý do khôi phục.")
            .MaximumLength(4000).WithMessage("Lý do tối đa 4000 ký tự.");
        RuleFor(x => x.DecisionNumber).NotEmpty().WithMessage("Cần nhập số quyết định.")
            .MaximumLength(100).WithMessage("Số quyết định tối đa 100 ký tự.");
        RuleFor(x => x.EffectiveDate).NotEmpty().WithMessage("Cần nhập ngày có hiệu lực.");
    }
}
public sealed class AdministrationHandlers(IProcedureAdministration store, IDocumentFormsClient forms) :
    IRequestHandler<ManagerDetailQuery, ProcedureResult<ProcedureDetailDto>>,
    IRequestHandler<SaveCategoryCommand, ProcedureResult<ProcedureCategoryDto>>,
    IRequestHandler<DeleteCategoryCommand, ProcedureResult<bool>>,
    IRequestHandler<DiscardDraftCommand, ProcedureResult<bool>>,
    IRequestHandler<RollbackProcedureCommand, ProcedureResult<ProcedureDetailDto>>,
    IRequestHandler<VersionSourceQuery, ProcedureResult<SourceLinkDto>>,
    IRequestHandler<DocumentFormsQuery, ProcedureResult<DocumentFormOptionDto[]>>
{
    public Task<ProcedureResult<ProcedureDetailDto>> Handle(ManagerDetailQuery r, CancellationToken ct) => store.Detail(r.Id, ct);
    public Task<ProcedureResult<bool>> Handle(DeleteCategoryCommand r, CancellationToken ct) => store.DeleteCategory(r.Id, ct);
    public Task<ProcedureResult<bool>> Handle(DiscardDraftCommand r, CancellationToken ct) => store.DiscardDraft(r.Id, ct);
    public Task<ProcedureResult<SourceLinkDto>> Handle(VersionSourceQuery r, CancellationToken ct) => store.VersionSource(r.Id, r.VersionId, ct);
    public async Task<ProcedureResult<ProcedureCategoryDto>> Handle(SaveCategoryCommand r, CancellationToken ct)
    {
        if (r.Input is null) return ProcedureResult<ProcedureCategoryDto>.Fail("validation.failed", "Thiếu thông tin danh mục.", 400);
        var validation = await new CategoryInputValidator().ValidateAsync(r.Input, ct);
        if (!validation.IsValid) return ProcedureResult<ProcedureCategoryDto>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return await store.SaveCategory(r.Id, r.Input, ct);
    }
    public async Task<ProcedureResult<ProcedureDetailDto>> Handle(RollbackProcedureCommand r, CancellationToken ct)
    {
        if (r.Input is null || r.VersionNumber < 1)
            return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Cần dữ liệu khôi phục và số phiên bản lớn hơn 0.", 400);
        var validation = await new RollbackInputValidator().ValidateAsync(r.Input, ct);
        if (!validation.IsValid) return ProcedureResult<ProcedureDetailDto>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return await store.Rollback(r, ct);
    }
    public Task<ProcedureResult<DocumentFormOptionDto[]>> Handle(DocumentFormsQuery r, CancellationToken ct) =>
        r.Page is < 1 or > 1000000 || r.PageSize is < 1 or > 100 || r.SearchCode?.Length > 100
            ? Task.FromResult(ProcedureResult<DocumentFormOptionDto[]>.Fail("validation.failed", "Trang từ 1 đến 1000000; số biểu mẫu từ 1 đến 100; mã tìm kiếm tối đa 100 ký tự.", 400))
            : forms.List(r, ct);
}
