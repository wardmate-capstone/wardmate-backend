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

public sealed record ConfigureOnlineFormCommand(Guid TemplateId, JsonElement SchemaDefinition,
    IReadOnlyList<DocxFieldMapping> Mappings, string OriginalSha256, string? CreatedBy = null) : ICommand<Guid>;

public sealed class ConfigureOnlineFormCommandHandler(IDocumentDbContext db, IMediator mediator,
    IFormSchemaEngine schemas, IDocxFormEngine docx) : ICommandHandler<ConfigureOnlineFormCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ConfigureOnlineFormCommand request, CancellationToken ct)
    {
        if (request.SchemaDefinition.ValueKind != JsonValueKind.Object)
            return DocumentFormErrors.InvalidSchema("schemaDefinition must be an object.");
        var parsed = schemas.ParseAndValidateSchema(request.SchemaDefinition.GetRawText());
        if (!parsed.IsSuccess) return parsed.Error;
        var fields = parsed.Value.GetAllFields().Select(f => f.FieldId).ToHashSet(StringComparer.Ordinal);
        if (request.Mappings is null || request.Mappings.Count == 0 || request.Mappings.Any(m => m is null)
            || !fields.SetEquals(request.Mappings.Select(m => m.FieldId)))
            return DocumentFormErrors.InvalidSchema("Map every schema field to the original DOCX; unknown fields are not allowed.");
        var template = await db.FormTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId && !t.IsDeleted, ct);
        if (template is null) return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        var download = await mediator.Send(new DownloadFormTemplateDocxQuery(request.TemplateId), ct);
        if (!download.IsSuccess) return download.Error;
        using var source = download.Value.FileStream;
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var hash = OnlineFormSupport.Hash(bytes);
        if (!string.Equals(hash, request.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            return Error.Conflict("document.original_changed", "The original changed. Reload docx-structure and configure again.");
        try { docx.Fill(bytes, request.Mappings, new Dictionary<string, string>()); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return DocumentFormErrors.InvalidSchema("Invalid DOCX mapping: " + ex.Message); }
        var number = (await db.FormTemplateVersions.Where(v => v.TemplateId == request.TemplateId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;
        var version = new FormTemplateVersion(request.TemplateId, number,
            request.SchemaDefinition.GetRawText(), request.CreatedBy);
        version.BindOriginal(template.FileDocxUrl!, hash, JsonSerializer.Serialize(request.Mappings));
        db.FormTemplateVersions.Add(version);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        { return Error.Conflict("document.configuration_conflict", "Another configuration was saved concurrently. Reload and retry."); }
        return version.Id;
    }
}

public sealed record GetDocxStructureQuery(Guid TemplateId) : IQuery<DocxStructureDto>;
public sealed record DocxStructureDto(string OriginalSha256, IReadOnlyList<DocxParagraph> Paragraphs);
public sealed class GetDocxStructureQueryHandler(IMediator mediator, IDocxFormEngine docx)
    : IQueryHandler<GetDocxStructureQuery, DocxStructureDto>
{
    public async Task<Result<DocxStructureDto>> Handle(GetDocxStructureQuery request, CancellationToken ct)
    {
        var result = await mediator.Send(new DownloadFormTemplateDocxQuery(request.TemplateId), ct);
        if (!result.IsSuccess) return result.Error;
        using var source = result.Value.FileStream;
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        try { return new DocxStructureDto(OnlineFormSupport.Hash(bytes), docx.Inspect(bytes)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return DocumentFormErrors.InvalidDocxFile("Unable to inspect this DOCX."); }
    }
}
