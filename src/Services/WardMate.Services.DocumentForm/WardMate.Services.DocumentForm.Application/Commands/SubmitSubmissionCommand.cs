using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// SubmitSubmission — Người dùng nộp hồ sơ chính thức (Draft → Submitted)
// POST /user-submissions/{id}/submit
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SubmitSubmissionCommand(
    Guid SubmissionId,
    Guid ApplicantId,
    string? SubmittedBy = null) : ICommand<SaveSubmissionResultDto>;

public sealed class SubmitSubmissionCommandHandler
    : ICommandHandler<SubmitSubmissionCommand, SaveSubmissionResultDto>
{
    private readonly IDocumentDbContext _dbContext;

    private readonly IFormSchemaEngine _schemas;

    public SubmitSubmissionCommandHandler(IDocumentDbContext dbContext, IFormSchemaEngine schemas)
    {
        _dbContext = dbContext;
        _schemas = schemas;
    }

    public async Task<Result<SaveSubmissionResultDto>> Handle(
        SubmitSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _dbContext.UserSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission is null)
            return DocumentFormErrors.SubmissionNotFound(request.SubmissionId);

        if (submission.ApplicantId != request.ApplicantId)
            return DocumentFormErrors.SubmissionNotOwnedByApplicant(submission.Id, request.ApplicantId);

        if (submission.TemplateVersionId is null || submission.FormData is null)
            return Error.Conflict("document.legacy_submission", "Create a new online draft for this legacy file-only submission.");
        var version = await _dbContext.FormTemplateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == submission.TemplateVersionId, cancellationToken);
        if (version is null) return Error.Conflict("document.version_missing", "The pinned template version is missing.");
        var schema = _schemas.ParseAndValidateSchema(version.SchemaDefinition);
        if (!schema.IsSuccess) return schema.Error;
        var validation = _schemas.ValidateFormData(schema.Value, submission.FormData);
        if (!validation.IsSuccess) return validation.Error;
        if (validation.Value.Count > 0) return DocumentFormErrors.InvalidFormData(JsonSerializer.Serialize(validation.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        try
        {
            submission.Submit(request.SubmittedBy);
        }
        catch (InvalidOperationException)
        {
            return DocumentFormErrors.SubmissionInvalidStatusTransition(submission.Status.ToString(), "Submit");
        }

        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        { return Error.Conflict("document.concurrent_edit", "The draft changed. Reload before submitting."); }

        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id,
            TemplateVersionId = submission.TemplateVersionId,
            Status = submission.Status.ToString(),
            FileName = submission.FileName,
            FileSizeBytes = submission.FileSizeBytes,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = "Electronic form finalized. Attach submissionId to the administrative application in the workflow service."
        };
    }
}
