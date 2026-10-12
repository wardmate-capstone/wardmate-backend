using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

public sealed record DownloadSupportingDocumentResult
{
    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
}

public sealed record DownloadSupportingDocumentQuery(Guid DocumentId)
    : IQuery<DownloadSupportingDocumentResult>;

public sealed class DownloadSupportingDocumentQueryHandler
    : IQueryHandler<DownloadSupportingDocumentQuery, DownloadSupportingDocumentResult>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public DownloadSupportingDocumentQueryHandler(
        IDocumentDbContext dbContext,
        IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<DownloadSupportingDocumentResult>> Handle(
        DownloadSupportingDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var doc = await _dbContext.SupportingDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId && !d.IsDeleted, cancellationToken);

        if (doc is null)
        {
            return DocumentFormErrors.SupportingDocumentNotFound(request.DocumentId);
        }

        var blobName = ExtractBlobName(doc.BlobUrl);
        var stream = await _blobClient.DownloadAsync(blobName, ct: cancellationToken);

        if (stream is null)
        {
            return DocumentFormErrors.SupportingDocumentNotFound(request.DocumentId);
        }

        return new DownloadSupportingDocumentResult
        {
            FileStream = stream,
            FileName = doc.FileName,
            ContentType = !string.IsNullOrWhiteSpace(doc.ContentType)
                ? doc.ContentType
                : "application/octet-stream"
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
