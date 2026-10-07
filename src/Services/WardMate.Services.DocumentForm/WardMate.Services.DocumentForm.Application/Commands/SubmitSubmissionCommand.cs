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

    private readonly WardMate.SharedKernel.Blob.IBlobStorageClient _blobs;

    public SubmitSubmissionCommandHandler(IDocumentDbContext dbContext, WardMate.SharedKernel.Blob.IBlobStorageClient blobs)
    {
        _dbContext = dbContext;
        _blobs = blobs;
    }

    public async Task<Result<SaveSubmissionResultDto>> Handle(
        SubmitSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _dbContext.UserSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, cancellationToken);

        if (submission is null)
            return DocumentFormErrors.SubmissionNotFound(request.SubmissionId);

        if (request.ApplicantId == Guid.Empty || submission.ApplicantId != request.ApplicantId)
            return DocumentFormErrors.SubmissionNotOwnedByApplicant(submission.Id, request.ApplicantId);

        if (!await _blobs.ExistsAsync(
            WardMate.Services.DocumentForm.Application.Services.OnlineFormSupport.BlobName(submission.BlobUrl), ct: cancellationToken))
            return DocumentFormErrors.SubmissionFileNotFound(submission.Id);
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
            Status = submission.Status.ToString(),
            FileName = submission.FileName,
            FileSizeBytes = submission.FileSizeBytes,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = "Electronic form finalized. Attach submissionId to the administrative application in the workflow service."
        };
    }
}
