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
        ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".heic", ".heif", ".webp"
    };

    // DOC-HEIC-01: ảnh iPhone (HEIC/HEIF) và WebP được chuyển sang JPEG trước khi lưu để trình duyệt cán bộ xem được.
    private static readonly HashSet<string> ConvertedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".heic", ".heif", ".webp" };
    private const int ConvertedMaxEdge = 3000;

    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;
    private readonly WardMate.Imaging.IImageProcessor _images;

    public UploadSupportingDocumentCommandHandler(
        IDocumentDbContext dbContext,
        IBlobStorageClient blobClient,
        WardMate.Imaging.IImageProcessor images)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
        _images = images;
    }

    /// <summary>Đổi HEIC/HEIF/WebP sang JPEG (xoay theo EXIF, bỏ metadata, cạnh dài ≤ 3000px); định dạng khác giữ nguyên.</summary>
    public static async Task<(Stream Stream, string FileName, string ContentType, long Size, Error? Error)> NormalizeImage(
        WardMate.Imaging.IImageProcessor images, Stream input, string fileName, string contentType, long size, CancellationToken ct)
    {
        if (!ConvertedExtensions.Contains(Path.GetExtension(fileName))) return (input, fileName, contentType, size, null);
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        try
        {
            var jpeg = images.ToJpeg(buffer.ToArray(), ConvertedMaxEdge);
            return (new MemoryStream(jpeg.Content), Path.ChangeExtension(fileName, ".jpg"), jpeg.ContentType, jpeg.Content.Length, null);
        }
        catch (WardMate.Imaging.ImageProcessingException error)
        {
            return (input, fileName, contentType, size, DocumentFormErrors.UnsupportedFileType(error.Message));
        }
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

        var normalized = await NormalizeImage(_images, request.FileStream, Path.GetFileName(request.FileName),
            request.ContentType, request.FileSizeBytes, cancellationToken);
        if (normalized.Error is not null) return normalized.Error;
        var sanitizedFileName = normalized.FileName;
        var blobPath = $"supporting-documents/{request.ApplicationId}/{Guid.NewGuid():N}_{sanitizedFileName}";

        string blobUrl;
        try
        {
            blobUrl = await _blobClient.UploadAsync(
                normalized.Stream,
                blobPath,
                normalized.ContentType,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            return DocumentFormErrors.BlobUploadFailed(ex.Message);
        }
        finally
        {
            if (!ReferenceEquals(normalized.Stream, request.FileStream)) await normalized.Stream.DisposeAsync();
        }

        var doc = new SupportingDocument(
            request.ApplicationId,
            sanitizedFileName,
            blobUrl,
            request.ChecklistId,
            normalized.Size,
            normalized.ContentType,
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
