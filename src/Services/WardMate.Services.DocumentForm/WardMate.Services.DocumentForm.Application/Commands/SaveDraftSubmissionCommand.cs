using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;
namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record SaveDraftSubmissionCommand(Guid? SubmissionId, Guid TemplateId, Guid ApplicantId,
    JsonElement FormData, Guid? TemplateVersionId = null, string? SavedBy = null) : ICommand<SaveSubmissionResultDto>;

public sealed class SaveDraftSubmissionCommandHandler(IDocumentDbContext db, IBlobStorageClient blobs,
    IFormSchemaEngine schemas, IDocxFormEngine docx) : ICommandHandler<SaveDraftSubmissionCommand, SaveSubmissionResultDto>
{
    public async Task<Result<SaveSubmissionResultDto>> Handle(SaveDraftSubmissionCommand request, CancellationToken ct)
    {
        if (request.ApplicantId == Guid.Empty || request.FormData.ValueKind != JsonValueKind.Object)
            return DocumentFormErrors.InvalidFormData("applicantId and a formData JSON object are required.");
        if (request.FormData.GetRawText().Length > 200000)
            return DocumentFormErrors.InvalidFormData("formData is too large.");
        UserSubmission? existing = null;
        if (request.SubmissionId.HasValue)
        {
            existing = await db.UserSubmissions.FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, ct);
            if (existing is null) return DocumentFormErrors.SubmissionNotFound(request.SubmissionId.Value);
            if (existing.ApplicantId != request.ApplicantId)
                return DocumentFormErrors.SubmissionNotOwnedByApplicant(existing.Id, request.ApplicantId);
            if (existing.Status != SubmissionStatus.Draft)
                return DocumentFormErrors.SubmissionInvalidStatusTransition(existing.Status.ToString(), "SaveDraft");
            if (existing.TemplateVersionId is null)
                return Error.Conflict("document.legacy_submission", "This legacy file-only draft cannot be edited online. Create a new draft.");
        }
        var templateId = existing?.TemplateId ?? request.TemplateId;
        var template = await db.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId && !t.IsDeleted, ct);
        if (template is null) return DocumentFormErrors.TemplateNotFound(templateId);
        if (!template.IsActive) return Error.Conflict("document.template_inactive", "The template is inactive.");
        var versionId = existing?.TemplateVersionId ?? request.TemplateVersionId;
        if (existing is not null && request.TemplateVersionId.HasValue && request.TemplateVersionId != versionId)
            return Error.Conflict("document.version_mismatch", "Use the draft's pinned templateVersionId.");
        var versions = db.FormTemplateVersions.AsNoTracking().Where(v => v.TemplateId == templateId && !v.IsDeleted);
        if (versionId.HasValue) versions = versions.Where(v => v.Id == versionId);
        else versions = versions.Where(v => v.OriginalBlobUrl == template.FileDocxUrl);
        var version = await versions.OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
        if (version is null || (existing is null && version.OriginalBlobUrl != template.FileDocxUrl))
            return Error.Conflict("document.template_not_configured", "Load a configured online template before creating a draft.");
        var parsed = schemas.ParseAndValidateSchema(version.SchemaDefinition);
        if (!parsed.IsSuccess) return parsed.Error;
        var validation = schemas.ValidateFormData(parsed.Value, request.FormData.GetRawText(), requireComplete: false);
        if (!validation.IsSuccess) return validation.Error;
        if (validation.Value.Count > 0)
            return DocumentFormErrors.InvalidFormData(JsonSerializer.Serialize(validation.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var original = await blobs.DownloadAsync(OnlineFormSupport.BlobName(version.OriginalBlobUrl), ct: ct);
        if (original is null) return DocumentFormErrors.FileNotUploaded;
        using var buffer = new MemoryStream();
        await original.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (OnlineFormSupport.Hash(bytes) != version.OriginalSha256)
            return Error.Conflict("document.original_changed", "The pinned original no longer matches its checksum.");
        byte[] generated;
        try { generated = docx.Fill(bytes, version.SchemaDefinition, OnlineFormSupport.Values(request.FormData)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return DocumentFormErrors.InvalidDocxFile("Cannot fill the configured original DOCX."); }
        var fileName = $"don-{template.Id:N}.docx";
        var blobName = $"user-submissions/{request.ApplicantId:N}/{Guid.NewGuid():N}.docx";
        string url;
        using var output = new MemoryStream(generated);
        try { url = await blobs.UploadAsync(output, blobName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ct: ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return DocumentFormErrors.BlobUploadFailed("Unable to store generated DOCX. Retry saving the draft."); }
        var submission = existing ?? new UserSubmission(templateId, request.ApplicantId, url, fileName, generated.Length, request.SavedBy);
        if (existing is not null) submission.SaveDraft(url, fileName, generated.Length, request.SavedBy);
        submission.SetOnlineData(version.Id, request.FormData.GetRawText());
        if (existing is null) db.UserSubmissions.Add(submission);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            await blobs.DeleteAsync(blobName, ct: CancellationToken.None);
            return Error.Conflict("document.concurrent_edit", "The draft changed. Reload before saving again.");
        }
        catch
        {
            await blobs.DeleteAsync(blobName, ct: CancellationToken.None);
            throw;
        }
        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id, TemplateVersionId = version.Id,
            Status = submission.Status.ToString(), FileName = fileName, FileSizeBytes = generated.Length,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = "Online draft saved; original DOCX preserved."
        };
    }
}
