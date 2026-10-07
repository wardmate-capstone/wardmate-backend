using Microsoft.Extensions.Logging;
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
    Stream FileStream, string FileName, long FileSizeBytes, string? SavedBy = null) : ICommand<SaveSubmissionResultDto>;

public sealed class SaveDraftSubmissionCommandHandler(IDocumentDbContext db, IBlobStorageClient blobs,
    Microsoft.Extensions.Logging.ILogger<SaveDraftSubmissionCommandHandler> logger) : ICommandHandler<SaveDraftSubmissionCommand, SaveSubmissionResultDto>
{
    public async Task<Result<SaveSubmissionResultDto>> Handle(SaveDraftSubmissionCommand request, CancellationToken ct)
    {
        if (request.ApplicantId == Guid.Empty)
            return DocumentFormErrors.InvalidFormData("applicantId is required.");
        var file = await DocxUpload.ReadAsync(request.FileStream, request.FileName, request.FileSizeBytes, ct);
        if (!file.IsSuccess) return file.Error;
        UserSubmission? existing = null;
        if (request.SubmissionId.HasValue)
        {
            existing = await db.UserSubmissions.FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, ct);
            if (existing is null) return DocumentFormErrors.SubmissionNotFound(request.SubmissionId.Value);
            if (existing.ApplicantId != request.ApplicantId)
                return DocumentFormErrors.SubmissionNotOwnedByApplicant(existing.Id, request.ApplicantId);
            if (existing.Status != SubmissionStatus.Draft)
                return DocumentFormErrors.SubmissionInvalidStatusTransition(existing.Status.ToString(), "SaveDraft");
        }
        var templateId = existing?.TemplateId ?? request.TemplateId;
        var template = await db.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId && !t.IsDeleted, ct);
        if (template is null) return DocumentFormErrors.TemplateNotFound(templateId);
        if (!template.IsActive) return Error.Conflict("document.template_inactive", "The template is inactive.");
        if (string.IsNullOrWhiteSpace(template.FileDocxUrl))
            return DocumentFormErrors.FileNotUploaded;
        var generated = file.Value;
        var fileName = Path.GetFileName(request.FileName.Replace('\\', '/'));
        var blobName = $"user-submissions/{request.ApplicantId:N}/{Guid.NewGuid():N}.docx";
        string url;
        using var output = new MemoryStream(generated);
        try { url = await blobs.UploadAsync(output, blobName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ct: ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return DocumentFormErrors.BlobUploadFailed("Unable to store edited DOCX. Retry saving the draft."); }
        var submission = existing ?? new UserSubmission(templateId, request.ApplicantId, url, fileName, generated.Length, request.SavedBy);
        if (existing is not null) submission.SaveDraft(url, fileName, generated.Length, request.SavedBy);
        if (existing is null) db.UserSubmissions.Add(submission);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            await CleanupAsync(blobName);
            return Error.Conflict("document.concurrent_edit", "The draft changed. Reload before saving again.");
        }
        catch
        {
            await CleanupAsync(blobName);
            throw;
        }
        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString(), FileName = fileName, FileSizeBytes = generated.Length,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = "Edited DOCX saved unchanged; original template preserved."
        };
    }

    private async Task CleanupAsync(string blobName)
    {
        try { await blobs.DeleteAsync(blobName, ct: CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to remove uncommitted draft blob."); }
    }
}
