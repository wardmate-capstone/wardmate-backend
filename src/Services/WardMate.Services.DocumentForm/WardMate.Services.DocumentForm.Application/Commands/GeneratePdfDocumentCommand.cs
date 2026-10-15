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


public sealed record GeneratePdfDocumentCommand(
    Guid ApplicationId,
    string DocumentType = "ORIGINAL_SUBMISSION",
    Guid? SubmissionId = null,
    Stream? FileStream = null,
    string? FileName = null,
    string? CreatedBy = null,
    Guid? CallerUserId = null,
    bool IsOfficer = false) : ICommand<GeneratedDocumentDto>;

public sealed class GeneratePdfDocumentCommandHandler
    : ICommandHandler<GeneratePdfDocumentCommand, GeneratedDocumentDto>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;
    private readonly IPdfRenderEngine _pdfRenderEngine;
    private readonly IDocxQrStamper _qrStamper;
    private readonly IApprovedQrService _qrService;

    public GeneratePdfDocumentCommandHandler(
        IDocumentDbContext dbContext,
        IBlobStorageClient blobClient,
        IPdfRenderEngine pdfRenderEngine,
        IDocxQrStamper qrStamper,
        IApprovedQrService qrService)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
        _pdfRenderEngine = pdfRenderEngine;
        _qrStamper = qrStamper;
        _qrService = qrService;
    }

    public async Task<Result<GeneratedDocumentDto>> Handle(
        GeneratePdfDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ApplicationId == Guid.Empty)
        {
            return Error.Validation("document.invalid_application_id", "Application ID cannot be empty.");
        }

        if (!request.SubmissionId.HasValue || request.SubmissionId == Guid.Empty)
            return Error.Validation("document.submission_required", "SubmissionId is required; it is different from ApplicationId.");
        var submission = await _dbContext.UserSubmissions.FirstOrDefaultAsync(s =>
            s.Id == request.SubmissionId && s.ApplicationId == request.ApplicationId && !s.IsDeleted, cancellationToken);
        if (submission is null) return DocumentFormErrors.SubmissionNotFound(request.SubmissionId.Value);
        if (request.FileStream is not null)
            return Error.Validation("document.use_saved_docx", "Save the DOCX to the submission before generating PDF.");

        // 2. Phân quyền và Kiểm tra Trạng thái phê duyệt
        // Nếu người gọi là người dân:
        // - Phải là chủ sở hữu hồ sơ (ApplicantId trùng khớp)
        // - Hồ sơ PHẢI có Status là Approved (đã được cán bộ duyệt)
        if (!request.IsOfficer)
        {
            if (!request.CallerUserId.HasValue || request.CallerUserId == Guid.Empty || submission.ApplicantId != request.CallerUserId)
            {
                return DocumentFormErrors.SubmissionNotOwnedByApplicant(submission.Id, request.CallerUserId ?? Guid.Empty);
            }

            if (submission.Status != SubmissionStatus.Approved)
            {
                return DocumentFormErrors.SubmissionNotApproved(submission.Id, submission.Status.ToString());
            }
        }
        if (submission.Status != SubmissionStatus.Approved)
            return DocumentFormErrors.SubmissionNotApproved(submission.Id, submission.Status.ToString());

        Stream? docxStream = null;
        var fileName = request.FileName;

        try
        {
            // 3. Lấy Stream DOCX
            if (request.FileStream is not null && request.FileStream.Length > 0)
            {
                docxStream = request.FileStream;
                fileName ??= submission.FileName;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(submission.BlobUrl))
                {
                    return DocumentFormErrors.SubmissionFileNotFound(submission.Id);
                }

                fileName ??= submission.FileName;
                var blobName = ExtractBlobName(submission.BlobUrl);
                docxStream = await _blobClient.DownloadAsync(blobName, ct: cancellationToken);

                if (docxStream is null)
                {
                    return DocumentFormErrors.SubmissionFileNotFound(submission.Id);
                }
            }

            fileName = string.IsNullOrWhiteSpace(fileName) ? "submission.docx" : fileName;

            // 4. Strip toàn bộ Word comments khỏi DOCX trước khi render PDF
            // Cán bộ có thể đã thêm comment/ghi chú lên DOCX để hướng dẫn người dân,
            // nhưng file PDF xuất ra cho người dân in phải sạch, không chứa comment nội bộ.
            using var cleanDocxStream = DocxCommentStripper.Strip(docxStream);

            // 4b. TASK-23: Chèn mã QR tra cứu vào cuối DOCX trước khi render PDF
            // Lấy QR đã được sinh khi approve (hoặc tự sinh nếu chưa có). Dùng stream tạm để không ảnh hưởng cleanDocxStream.
            var approvedQr = await _dbContext.ApprovedQrs
                .FirstOrDefaultAsync(q => q.ApplicationId == request.ApplicationId, cancellationToken);

            if (approvedQr is null)
            {
                approvedQr = _qrService.Generate(request.ApplicationId, submission.Id);
                _dbContext.ApprovedQrs.Add(approvedQr);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            Stream docxForRender = _qrStamper.StampQr(cleanDocxStream, approvedQr.QrImagePng);

            // 5. Chuyển đổi DOCX sang PDF bằng Render Engine
            Stream pdfStream;
            try
            {
                pdfStream = await _pdfRenderEngine.RenderDocxToPdfAsync(docxForRender, fileName, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (approvedQr is not null && !ReferenceEquals(docxForRender, cleanDocxStream))
                    await docxForRender.DisposeAsync();
                return DocumentFormErrors.PdfRenderFailed("Dịch vụ chuyển đổi chưa sẵn sàng. Vui lòng thử lại.");
            }
            if (approvedQr is not null && !ReferenceEquals(docxForRender, cleanDocxStream))
                await docxForRender.DisposeAsync();

            using var renderedPdf = pdfStream;

            // 3. Upload file PDF kết xuất lên Azure Blob Storage
            var sanitizedType = string.IsNullOrWhiteSpace(request.DocumentType)
                ? "ORIGINAL_SUBMISSION"
                : request.DocumentType.Trim().ToUpperInvariant();

            var blobPath = $"generated-documents/{request.ApplicationId}/{sanitizedType.ToLowerInvariant()}_{Guid.NewGuid():N}.pdf";

            string pdfBlobUrl;
            try
            {
                pdfBlobUrl = await _blobClient.UploadAsync(
                    pdfStream,
                    blobPath,
                    "application/pdf",
                    ct: cancellationToken);
            }
            catch (Exception ex)
            {
                return DocumentFormErrors.BlobUploadFailed(ex.Message);
            }

            // 4. Lưu hoặc cập nhật bản ghi GeneratedDocument vào DB
            // Each result has its own immutable record; one form must not overwrite another form's PDF.
            var generatedDocument = new GeneratedDocument(request.ApplicationId, sanitizedType, pdfBlobUrl, request.CreatedBy);
            _dbContext.GeneratedDocuments.Add(generatedDocument);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new GeneratedDocumentDto
            {
                Id = generatedDocument.Id,
                ApplicationId = generatedDocument.ApplicationId,
                DocumentType = generatedDocument.DocumentType,
                PdfBlobUrl = generatedDocument.PdfBlobUrl,
                CreatedAtUtc = generatedDocument.CreatedAtUtc
            };
        }
        finally
        {
            if (docxStream is not null && docxStream != request.FileStream)
            {
                await docxStream.DisposeAsync();
            }
        }
    }

    private static string ExtractBlobName(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var path = uri.AbsolutePath.TrimStart('/');
        if (path.StartsWith("devstoreaccount1/", StringComparison.OrdinalIgnoreCase))
        {
            path = path["devstoreaccount1/".Length..];
        }
        var firstSlashIndex = path.IndexOf('/');
        return Uri.UnescapeDataString(firstSlashIndex >= 0 ? path[(firstSlashIndex + 1)..] : path);
    }
}
