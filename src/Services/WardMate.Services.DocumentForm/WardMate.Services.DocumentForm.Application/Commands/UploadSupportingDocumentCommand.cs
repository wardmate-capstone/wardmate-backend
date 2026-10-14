using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record UploadSupportingDocumentCommand(
    Guid ApplicationId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Guid? ChecklistId = null,
    string? CreatedBy = null, string? SubmissionType = null, string? DocumentCopyType = null,
    int Quantity = 1) : ICommand<SupportingDocumentDto>;

public sealed class UploadSupportingDocumentCommandHandler
    : ICommandHandler<UploadSupportingDocumentCommand, SupportingDocumentDto>
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx"
    };

    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public UploadSupportingDocumentCommandHandler(
        IDocumentDbContext dbContext,
        IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<SupportingDocumentDto>> Handle(
        UploadSupportingDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity < 1 || request.SubmissionType is not (null or "NOP" or "XUAT_TRINH")
            || request.DocumentCopyType is not (null or "ORIGINAL" or "CERTIFIED_COPY" or "REGULAR_COPY"))
            return Error.Validation("document.invalid_evidence", "Số lượng phải lớn hơn 0; loại bản sao và hình thức nộp/xuất trình không hợp lệ.");
        if (request.ApplicationId == Guid.Empty)
        {
            return Error.Validation("document.invalid_application_id", "Application ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return DocumentFormErrors.EmptyFile;
        }

        if (request.FileSizeBytes <= 0)
        {
            return DocumentFormErrors.EmptyFile;
        }

        if (request.FileSizeBytes > MaxFileSizeBytes)
        {
            return DocumentFormErrors.FileTooLarge(MaxFileSizeBytes);
        }

        var ext = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return DocumentFormErrors.UnsupportedFileType(
                $"File extension '{ext}' is not supported. Allowed extensions: {string.Join(", ", AllowedExtensions)}.");
        }

        var sanitizedFileName = Path.GetFileName(request.FileName);
        var blobPath = $"supporting-documents/{request.ApplicationId}/{Guid.NewGuid():N}_{sanitizedFileName}";

        string blobUrl;
        try
        {
            blobUrl = await _blobClient.UploadAsync(
                request.FileStream,
                blobPath,
                request.ContentType,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            return DocumentFormErrors.BlobUploadFailed(ex.Message);
        }

        var doc = new SupportingDocument(
            request.ApplicationId,
            sanitizedFileName,
            blobUrl,
            request.ChecklistId,
            request.FileSizeBytes,
            request.ContentType,
            request.CreatedBy);

        doc.DeclareEvidence(request.SubmissionType, request.DocumentCopyType, request.Quantity);
        _dbContext.SupportingDocuments.Add(doc);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SupportingDocumentDto
        {
            Id = doc.Id,
            SubmissionType = doc.SubmissionType, DocumentCopyType = doc.DocumentCopyType, Quantity = doc.Quantity,
            ApplicationId = doc.ApplicationId,
            ChecklistId = doc.ChecklistId,
            FileName = doc.FileName,
            BlobUrl = doc.BlobUrl,
            FileSizeBytes = doc.FileSizeBytes,
            ContentType = doc.ContentType,
            CreatedAtUtc = doc.CreatedAtUtc
        };
    }
}
