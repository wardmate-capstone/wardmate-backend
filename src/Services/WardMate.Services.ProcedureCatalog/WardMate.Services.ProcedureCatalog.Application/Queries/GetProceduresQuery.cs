using FluentValidation;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.Application.Queries;

// IsActive is supplied explicitly by the controller; the public endpoint always supplies true.
public sealed record GetProceduresQuery(int Page = 1, int PageSize = 20, string? Search = null,
    int? CategoryId = null, bool? IsActive = true) : IRequest<ProcedureResult<ProcedureListDto>>;

public sealed class GetProceduresValidator : AbstractValidator<GetProceduresQuery>
{
    public GetProceduresValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
        RuleFor(x => x).Must(x => ((long)x.Page - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("Số trang vượt quá giới hạn hỗ trợ.").OverridePropertyName("Page");
        RuleFor(x => x.Search).MaximumLength(255).WithMessage("Từ khóa tìm kiếm không được vượt quá 255 ký tự.");
        RuleFor(x => x.CategoryId).GreaterThan(0).When(x => x.CategoryId.HasValue)
            .WithMessage("Mã danh mục phải lớn hơn 0.");
    }
}

public sealed class GetProceduresHandler(IProcedureRepository repository)
    : IRequestHandler<GetProceduresQuery, ProcedureResult<ProcedureListDto>>
{
    public async Task<ProcedureResult<ProcedureListDto>> Handle(GetProceduresQuery request, CancellationToken ct)
    {
        var validation = await new GetProceduresValidator().ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ProcedureResult<ProcedureListDto>.Fail("validation.failed", "Dữ liệu không hợp lệ.", 400, validation.ToDictionary());
        return ProcedureResult<ProcedureListDto>.Ok(await repository.List(request, ct));
    }
}
