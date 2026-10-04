using Microsoft.EntityFrameworkCore;
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

    public SubmitSubmissionCommandHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
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

        try
        {
            submission.Submit(request.SubmittedBy);
        }
        catch (InvalidOperationException)
        {
            return DocumentFormErrors.SubmissionInvalidStatusTransition(submission.Status.ToString(), "Submit");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString(),
            FileName = submission.FileName,
            FileSizeBytes = submission.FileSizeBytes,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = "Submission submitted successfully. Waiting for officer review."
        };
    }
}
