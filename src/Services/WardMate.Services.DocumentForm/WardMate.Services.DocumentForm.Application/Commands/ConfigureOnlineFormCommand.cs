using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;
namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record ConfigureOnlineFormCommand(Guid TemplateId, JsonElement SchemaDefinition, string? CreatedBy = null) : ICommand<Guid>;

public sealed class ConfigureOnlineFormCommandHandler(IDocumentDbContext db, IFormSchemaEngine schemas) : ICommandHandler<ConfigureOnlineFormCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ConfigureOnlineFormCommand request, CancellationToken ct)
    {
        if (request.SchemaDefinition.ValueKind != JsonValueKind.Object)
            return DocumentFormErrors.InvalidSchema("schemaDefinition must be an object.");
        var parsed = schemas.ParseAndValidateSchema(request.SchemaDefinition.GetRawText());
        if (!parsed.IsSuccess) return parsed.Error;
        
        var template = await db.FormTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId && !t.IsDeleted, ct);
        if (template is null) return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        
        // Ensure a DOCX file has been uploaded
        if (string.IsNullOrEmpty(template.FileDocxUrl))
            return Error.Conflict("document.no_docx", "Please upload a DOCX file before configuring online schema.");

        var number = (await db.FormTemplateVersions.Where(v => v.TemplateId == request.TemplateId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;
            
        var version = new FormTemplateVersion(request.TemplateId, number,
            request.SchemaDefinition.GetRawText(), request.CreatedBy);
            
        // We no longer require rigid byte mappings. The original DOCX is preserved, and mapping is auto-inferred at fill time.
        version.BindOriginal(template.FileDocxUrl, "auto-fill", "[]");
        
        db.FormTemplateVersions.Add(version);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        { return Error.Conflict("document.configuration_conflict", "Another configuration was saved concurrently. Reload and retry."); }
        
        return version.Id;
    }
}
