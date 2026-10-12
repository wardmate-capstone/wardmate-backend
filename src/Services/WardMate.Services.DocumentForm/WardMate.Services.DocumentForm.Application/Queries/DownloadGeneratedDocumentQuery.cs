using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record DownloadGeneratedDocumentResult
{
    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/pdf";
}

public sealed record DownloadGeneratedDocumentQuery(
    Guid DocumentId,
    Guid? CallerUserId = null,
    bool IsOfficer = false)
    : IQuery<DownloadGeneratedDocumentResult>;

public sealed class DownloadGeneratedDocumentQueryHandler
    : IQueryHandler<DownloadGeneratedDocumentQuery, DownloadGeneratedDocumentResult>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public DownloadGeneratedDocumentQueryHandler(
        IDocumentDbContext dbContext,
        IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<DownloadGeneratedDocumentResult>> Handle(
        DownloadGeneratedDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var doc = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId && !d.IsDeleted, cancellationToken);

        if (doc is null)
        {
            return DocumentFormErrors.GeneratedDocumentNotFound(request.DocumentId);
        }

        // Kiểm tra quyền sở hữu & trạng thái phê duyệt nếu người gọi là người dân
        if (!request.IsOfficer && request.CallerUserId.HasValue && request.CallerUserId.Value != Guid.Empty)
        {
            var submission = await _dbContext.UserSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => (s.Id == doc.ApplicationId || s.ApplicantId == doc.ApplicationId) && !s.IsDeleted, cancellationToken);

            if (submission is not null)
            {
                if (submission.ApplicantId != request.CallerUserId.Value)
                {
                    return DocumentFormErrors.SubmissionNotOwnedByApplicant(submission.Id, request.CallerUserId.Value);
                }

                if (submission.Status != SubmissionStatus.Approved)
                {
                    return DocumentFormErrors.SubmissionNotApproved(submission.Id, submission.Status.ToString());
                }
            }
        }

        var blobName = ExtractBlobName(doc.PdfBlobUrl);
        var stream = await _blobClient.DownloadAsync(blobName, ct: cancellationToken);

        if (stream is null)
        {
            return DocumentFormErrors.GeneratedDocumentNotFound(request.DocumentId);
        }

        var fileName = $"{doc.DocumentType.ToLowerInvariant()}_{doc.ApplicationId:N}.pdf";

        return new DownloadGeneratedDocumentResult
        {
            FileStream = stream,
            FileName = fileName,
            ContentType = "application/pdf"
        };
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
