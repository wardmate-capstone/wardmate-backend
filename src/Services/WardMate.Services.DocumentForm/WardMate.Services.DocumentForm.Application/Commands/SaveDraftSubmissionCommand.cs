using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// SaveDraftSubmission — Người dùng lưu nháp (tạo mới hoặc cập nhật)
// POST /user-submissions/draft       → Tạo mới (submissionId = null)
// PUT  /user-submissions/{id}/draft  → Cập nhật nháp đang có
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SaveDraftSubmissionCommand(
    Guid? SubmissionId,      // null = tạo mới; có giá trị = cập nhật nháp
    Guid TemplateId,
    Guid ApplicantId,
    Stream FileStream,
    string FileName,
    long FileSizeBytes,
    string? SavedBy = null) : ICommand<SaveSubmissionResultDto>;

public sealed class SaveDraftSubmissionCommandHandler
    : ICommandHandler<SaveDraftSubmissionCommand, SaveSubmissionResultDto>
{
    private const long MaxDocxSizeBytes = 20 * 1024 * 1024; // 20 MB
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public SaveDraftSubmissionCommandHandler(IDocumentDbContext dbContext, IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<SaveSubmissionResultDto>> Handle(
        SaveDraftSubmissionCommand request, CancellationToken cancellationToken)
    {
        // ── Validate file ─────────────────────────────────────────────────
        if (request.FileSizeBytes <= 0)
            return DocumentFormErrors.EmptyFile;

        if (request.FileSizeBytes > MaxDocxSizeBytes)
            return DocumentFormErrors.FileTooLarge(MaxDocxSizeBytes);

        if (!string.Equals(Path.GetExtension(request.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
            return DocumentFormErrors.InvalidDocxFile("Only .docx files are accepted for submission.");

        // ── Upload file lên Blob Storage ──────────────────────────────────
        using var memoryStream = new MemoryStream();
        await request.FileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var blobPath = $"user-submissions/{request.ApplicantId:N}/{Guid.NewGuid():N}_{Path.GetFileName(request.FileName)}";
        var blobUrl = await _blobClient.UploadAsync(
            memoryStream,
            blobPath,
            DocxContentType,
            ct: cancellationToken);

        UserSubmission submission;

        if (request.SubmissionId.HasValue)
        {
            // ── Cập nhật nháp đang có ────────────────────────────────────
            var existing = await _dbContext.UserSubmissions
                .FirstOrDefaultAsync(s => s.Id == request.SubmissionId.Value, cancellationToken);

            if (existing is null)
                return DocumentFormErrors.SubmissionNotFound(request.SubmissionId.Value);

            if (existing.ApplicantId != request.ApplicantId)
                return DocumentFormErrors.SubmissionNotOwnedByApplicant(existing.Id, request.ApplicantId);

            try
            {
                existing.SaveDraft(blobUrl, request.FileName, request.FileSizeBytes, request.SavedBy);
            }
            catch (InvalidOperationException)
            {
                return DocumentFormErrors.SubmissionInvalidStatusTransition(existing.Status.ToString(), "SaveDraft");
            }

            submission = existing;
        }
        else
        {
            // ── Tạo mới bản nháp ─────────────────────────────────────────
            submission = new UserSubmission(
                request.TemplateId,
                request.ApplicantId,
                blobUrl,
                request.FileName,
                request.FileSizeBytes,
                request.SavedBy);

            _dbContext.UserSubmissions.Add(submission);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString(),
            FileName = submission.FileName,
            FileSizeBytes = submission.FileSizeBytes,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = request.SubmissionId.HasValue
                ? "Draft updated successfully."
                : "Draft created successfully."
        };
    }
}
