using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

/// <summary>
/// Cán bộ lưu lại file DOCX đã được mở trên web editor và thêm comment/ghi chú.
/// DOCX mới (có comment) sẽ thay thế bản hiện có trong BlobStorage.
/// Status hồ sơ KHÔNG thay đổi — cán bộ vẫn cần gọi riêng request-revision hoặc approve.
/// Người dân khi mở lại DOCX (download-docx) sẽ thấy toàn bộ comment của cán bộ.
/// </summary>
public sealed record SaveOfficerAnnotatedDocxCommand(
    Guid SubmissionId,
    Stream FileStream,
    string FileName,
    long FileSizeBytes,
    Guid OfficerId,
    string? SavedBy = null) : ICommand<SaveSubmissionResultDto>;

public sealed class SaveOfficerAnnotatedDocxCommandHandler(
    IDocumentDbContext db,
    IBlobStorageClient blobs,
    ILogger<SaveOfficerAnnotatedDocxCommandHandler> logger)
    : ICommandHandler<SaveOfficerAnnotatedDocxCommand, SaveSubmissionResultDto>
{
    public async Task<Result<SaveSubmissionResultDto>> Handle(
        SaveOfficerAnnotatedDocxCommand request, CancellationToken ct)
    {
        if (request.OfficerId == Guid.Empty)
            return DocumentFormErrors.InvalidFormData("officerId is required.");

        // 1. Validate file DOCX
        var file = await DocxUpload.ReadAsync(request.FileStream, request.FileName, request.FileSizeBytes, ct);
        if (!file.IsSuccess) return file.Error;

        // 2. Tìm hồ sơ
        var submission = await db.UserSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, ct);

        if (submission is null)
            return DocumentFormErrors.SubmissionNotFound(request.SubmissionId);

        // 3. Chỉ cho phép annotate khi hồ sơ đang là Submitted hoặc RevisionRequested
        if (submission.Status != SubmissionStatus.Submitted && submission.Status != SubmissionStatus.RevisionRequested)
            return DocumentFormErrors.SubmissionInvalidStatusTransition(
                submission.Status.ToString(), "AnnotateDocx");

        // 4. Upload DOCX có comment lên BlobStorage (overwrite bằng path mới)
        var bytes = file.Value;
        var fileName = Path.GetFileName(request.FileName.Replace('\\', '/'));
        var blobName = $"officer-annotations/{request.SubmissionId:N}/{Guid.NewGuid():N}.docx";

        string url;
        using var output = new MemoryStream(bytes);
        try
        {
            url = await blobs.UploadAsync(
                output, blobName,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to upload annotated DOCX for submission {Id}", request.SubmissionId);
            return DocumentFormErrors.BlobUploadFailed("Không thể lưu file DOCX có ghi chú. Vui lòng thử lại.");
        }

        // 5. Cập nhật BlobUrl mà không đổi Status
        try
        {
            submission.AnnotateDocx(url, fileName, bytes.LongLength, request.SavedBy);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Rollback blob nếu DB fail
            try { await blobs.DeleteAsync(blobName, ct: CancellationToken.None); } catch { /* ignore */ }
            throw;
        }

        return new SaveSubmissionResultDto
        {
            SubmissionId = submission.Id,
            Status = submission.Status.ToString(),
            FileName = fileName,
            FileSizeBytes = bytes.LongLength,
            UpdatedAt = submission.UpdatedAtUtc ?? submission.CreatedAtUtc,
            Message = "DOCX đã được lưu kèm ghi chú của cán bộ. Người dân sẽ thấy comment khi mở lại file."
        };
    }
}
