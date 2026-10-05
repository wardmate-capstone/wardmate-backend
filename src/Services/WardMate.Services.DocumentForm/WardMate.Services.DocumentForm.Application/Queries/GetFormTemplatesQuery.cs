using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record GetFormTemplatesQuery(
    int Page = 1,
    int PageSize = 20,
    bool? IsActive = null,
    string? SearchCode = null) : IQuery<PagedResult<FormTemplateDto>>;

public sealed class GetFormTemplatesQueryHandler : IQueryHandler<GetFormTemplatesQuery, PagedResult<FormTemplateDto>>
{
    private readonly IDocumentDbContext _dbContext;

    public GetFormTemplatesQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<FormTemplateDto>>> Handle(GetFormTemplatesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _dbContext.FormTemplates
            .AsNoTracking()
            .AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchCode))
        {
            var normalized = request.SearchCode.Trim().ToUpperInvariant();
            query = query.Where(t => t.Code.Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(t => new FormTemplateDto
        {
            Id = t.Id,
            Code = t.Code,
            Title = t.Title,
            FileDocxUrl = t.FileDocxUrl,
            IsActive = t.IsActive,
            CreatedAtUtc = t.CreatedAtUtc,
            UpdatedAtUtc = t.UpdatedAtUtc
        }).ToList();

        return PagedResult<FormTemplateDto>.Create(dtos, totalCount, page, pageSize);
    }
}
