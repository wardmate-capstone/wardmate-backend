using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record CreateFormTemplateVersionCommand(
    Guid TemplateId,
    string SchemaDefinition,
    string? CreatedBy = null) : ICommand<FormTemplateVersionDto>;

public sealed class CreateFormTemplateVersionCommandHandler : ICommandHandler<CreateFormTemplateVersionCommand, FormTemplateVersionDto>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IFormSchemaEngine _schemaEngine;

    public CreateFormTemplateVersionCommandHandler(IDocumentDbContext dbContext, IFormSchemaEngine schemaEngine)
    {
        _dbContext = dbContext;
        _schemaEngine = schemaEngine;
    }

    public async Task<Result<FormTemplateVersionDto>> Handle(CreateFormTemplateVersionCommand request, CancellationToken cancellationToken)
    {
        var validateSchemaResult = _schemaEngine.ParseAndValidateSchema(request.SchemaDefinition);
        if (!validateSchemaResult.IsSuccess)
        {
            return validateSchemaResult.Error;
        }

        var template = await _dbContext.FormTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        }

        // Calculate version locally
        var nextVersionNumber = template.Versions.Count == 0 ? 1 : template.Versions.Max(v => v.VersionNumber) + 1;
        var newVersion = new FormTemplateVersion(template.Id, nextVersionNumber, request.SchemaDefinition, request.CreatedBy);
        
        // 1. Update template
        template.SetUpdated(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // 2. Insert version
        _dbContext.FormTemplateVersions.Add(newVersion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new FormTemplateVersionDto
        {
            Id = newVersion.Id,
            TemplateId = newVersion.TemplateId,
            VersionNumber = newVersion.VersionNumber,
            SchemaDefinition = newVersion.SchemaDefinition,
            CreatedAtUtc = newVersion.CreatedAtUtc,
            UpdatedAtUtc = newVersion.UpdatedAtUtc
        };

        return dto;
    }
}
