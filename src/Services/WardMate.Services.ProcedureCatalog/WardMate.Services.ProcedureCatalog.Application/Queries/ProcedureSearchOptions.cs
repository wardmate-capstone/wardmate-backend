using FluentValidation;
using MediatR;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.Application.Queries;

public sealed record ProcedureSearchOptions(string? Keyword = null, int? CategoryId = null,
    string? LevelOfImplementation = null, bool? IsActive = true, int PageNumber = 1, int PageSize = 10,
    string SortBy = "Title", bool IsAscending = true);

public sealed class ProcedureSearchValidator : AbstractValidator<ProcedureSearchOptions>
{
    public ProcedureSearchValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
        RuleFor(x => x).Must(x => ((long)x.PageNumber - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("Số trang vượt quá giới hạn hỗ trợ.").OverridePropertyName("PageNumber");
        RuleFor(x => x.Keyword).MaximumLength(255).WithMessage("Từ khóa không được vượt quá 255 ký tự.");
        RuleFor(x => x.LevelOfImplementation).MaximumLength(50).WithMessage("Cấp thực hiện không được vượt quá 50 ký tự.");
        RuleFor(x => x.CategoryId).GreaterThan(0).When(x => x.CategoryId.HasValue).WithMessage("Mã danh mục phải lớn hơn 0.");
        RuleFor(x => x.SortBy).Must(x => new[] { "Title", "ProcedureCode", "UpdatedAt", "CreatedAt", "LevelOfImplementation" }
            .Contains(x, StringComparer.OrdinalIgnoreCase)).WithMessage("SortBy phải là Title, ProcedureCode, UpdatedAt, CreatedAt hoặc LevelOfImplementation.");
    }
}


