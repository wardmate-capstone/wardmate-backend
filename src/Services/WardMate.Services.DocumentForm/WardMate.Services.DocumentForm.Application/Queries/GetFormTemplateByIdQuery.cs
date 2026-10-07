using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record GetFormTemplateByIdQuery(Guid TemplateId) : IQuery<FormTemplateDetailDto>;

public sealed class GetFormTemplateByIdQueryHandler : IQueryHandler<GetFormTemplateByIdQuery, FormTemplateDetailDto>
{
    private readonly IDocumentDbContext _dbContext;

    public GetFormTemplateByIdQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<FormTemplateDetailDto>> Handle(GetFormTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.FormTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        }

        var version = await _dbContext.FormTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == template.Id && v.OriginalBlobUrl == template.FileDocxUrl && !v.IsDeleted)
            .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        var dto = new FormTemplateDetailDto
        {
            Id = template.Id,
            TemplateVersionId = version?.Id,
            VersionNumber = version?.VersionNumber,
            SchemaDefinition = version is null ? null : System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(version.SchemaDefinition),
            OnlineReady = template.IsActive && version is not null,
            Code = template.Code,
            Title = template.Title,
            FileDocxUrl = template.FileDocxUrl,
            IsActive = template.IsActive,
            CreatedAtUtc = template.CreatedAtUtc,
            UpdatedAtUtc = template.UpdatedAtUtc
        };

        return dto;
    }
}
