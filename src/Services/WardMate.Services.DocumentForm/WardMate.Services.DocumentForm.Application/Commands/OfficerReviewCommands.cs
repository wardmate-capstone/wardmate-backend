using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// RequestRevision — Cán bộ yêu cầu sửa lại kèm comment (Submitted → RevisionRequested)
// POST /api/v1/officer/submissions/{id}/request-revision
// ─────────────────────────────────────────────────────────────────────────────

public sealed record RequestRevisionCommand(
    Guid SubmissionId,
    Guid OfficerId,
    string Comment,
    string? ReviewedBy = null) : ICommand<SaveSubmissionResultDto>;

public sealed class RequestRevisionCommandHandler
    : ICommandHandler<RequestRevisionCommand, SaveSubmissionResultDto>
{
    private readonly IDocumentDbContext _dbContext;

    public RequestRevisionCommandHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<SaveSubmissionResultDto>> Handle(
        RequestRevisionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _dbContext.UserSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, cancellationToken);

        if (submission is null)
            return DocumentFormErrors.SubmissionNotFound(request.SubmissionId);

        try
        {
            submission.RequestRevision(request.OfficerId, request.Comment, request.ReviewedBy);
        }
        catch (InvalidOperationException)
        {
            return DocumentFormErrors.SubmissionInvalidStatusTransition(submission.Status.ToString(), "RequestRevision");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString(),
            FileName = submission.FileName,
            FileSizeBytes = submission.FileSizeBytes,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = "Revision requested. The applicant has been notified to correct their submission."
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ApproveSubmission — Cán bộ duyệt hồ sơ (Submitted → Approved)
// POST /api/v1/officer/submissions/{id}/approve
// Sau khi chuyển trạng thái, tự động sinh mã QR tra cứu và lưu vào document.approved_qrs.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record ApproveSubmissionCommand(
    Guid SubmissionId,
    Guid OfficerId,
    string? ReviewedBy = null) : ICommand<SaveSubmissionResultDto>;

public sealed class ApproveSubmissionCommandHandler
    : ICommandHandler<ApproveSubmissionCommand, SaveSubmissionResultDto>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IApprovedQrService _qrService;

    public ApproveSubmissionCommandHandler(
        IDocumentDbContext dbContext,
        IApprovedQrService qrService)
    {
        _dbContext = dbContext;
        _qrService = qrService;
    }

    public async Task<Result<SaveSubmissionResultDto>> Handle(
        ApproveSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await _dbContext.UserSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, cancellationToken);

        if (submission is null)
            return DocumentFormErrors.SubmissionNotFound(request.SubmissionId);

        try
        {
            submission.Approve(request.OfficerId, request.ReviewedBy);
        }
        catch (InvalidOperationException)
        {
            return DocumentFormErrors.SubmissionInvalidStatusTransition(submission.Status.ToString(), "Approve");
        }

        // TASK-22: Sinh mã QR tra cứu khi hồ sơ có applicationId liên kết
        string? qrCodeValue = null;
        if (submission.ApplicationId is { } applicationId && applicationId != Guid.Empty)
        {
            // Idempotent: chỉ tạo QR nếu chưa tồn tại cho application này
            var existingQr = await _dbContext.ApprovedQrs
                .FirstOrDefaultAsync(q => q.ApplicationId == applicationId, cancellationToken);

            if (existingQr is null)
            {
                var qr = _qrService.Generate(applicationId, submission.Id);
                _dbContext.ApprovedQrs.Add(qr);
                qrCodeValue = qr.QrCodeValue;
            }
            else
            {
                qrCodeValue = existingQr.QrCodeValue;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString(),
            FileName = submission.FileName,
            FileSizeBytes = submission.FileSizeBytes,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            QrCodeValue = qrCodeValue,
            Message = "Submission approved. The applicant can now export and print the official PDF."
        };
    }
}
