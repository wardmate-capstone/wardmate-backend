using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record CreateFormTemplateCommand(
    string Code,
    string Title,
    string? InitialSchemaDefinition = null,
    string? CreatedBy = null) : ICommand<FormTemplateDetailDto>;

public sealed class CreateFormTemplateCommandHandler : ICommandHandler<CreateFormTemplateCommand, FormTemplateDetailDto>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IFormSchemaEngine _schemaEngine;

    public CreateFormTemplateCommandHandler(IDocumentDbContext dbContext, IFormSchemaEngine schemaEngine)
    {
        _dbContext = dbContext;
        _schemaEngine = schemaEngine;
    }

    public async Task<Result<FormTemplateDetailDto>> Handle(CreateFormTemplateCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var existing = await _dbContext.FormTemplates
            .AnyAsync(t => t.Code == normalizedCode, cancellationToken);

        if (existing)
        {
            return DocumentFormErrors.TemplateCodeAlreadyExists(normalizedCode);
        }

        var template = new FormTemplate(normalizedCode, request.Title, createdBy: request.CreatedBy);

        if (!string.IsNullOrWhiteSpace(request.InitialSchemaDefinition))
        {
            var validateSchemaResult = _schemaEngine.ParseAndValidateSchema(request.InitialSchemaDefinition);
            if (!validateSchemaResult.IsSuccess)
            {
                return validateSchemaResult.Error;
            }

            template.AddVersion(request.InitialSchemaDefinition, request.CreatedBy);
        }

        _dbContext.FormTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new FormTemplateDetailDto
        {
            Id = template.Id,
            Code = template.Code,
            Title = template.Title,
            FileDocxUrl = template.FileDocxUrl,
            IsActive = template.IsActive,
            CreatedAtUtc = template.CreatedAtUtc,
            UpdatedAtUtc = template.UpdatedAtUtc,
            Versions = template.Versions.Select(v => new FormTemplateVersionDto
            {
                Id = v.Id,
                TemplateId = v.TemplateId,
                VersionNumber = v.VersionNumber,
                SchemaDefinition = v.SchemaDefinition,
                CreatedAtUtc = v.CreatedAtUtc,
                UpdatedAtUtc = v.UpdatedAtUtc
            }).ToList()
        };

        return dto;
    }
}
