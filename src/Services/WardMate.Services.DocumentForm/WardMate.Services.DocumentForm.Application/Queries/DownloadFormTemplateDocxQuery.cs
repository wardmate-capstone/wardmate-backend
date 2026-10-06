using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

// === Download: tra ve binary stream ===

public sealed record DownloadDocxResult
{
    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}

public sealed record DownloadFormTemplateDocxQuery(Guid TemplateId) : IQuery<DownloadDocxResult>;

public sealed class DownloadFormTemplateDocxQueryHandler : IQueryHandler<DownloadFormTemplateDocxQuery, DownloadDocxResult>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public DownloadFormTemplateDocxQueryHandler(IDocumentDbContext dbContext, IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<DownloadDocxResult>> Handle(DownloadFormTemplateDocxQuery request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (template is null) return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        if (string.IsNullOrWhiteSpace(template.FileDocxUrl)) return DocumentFormErrors.FileNotUploaded;
        var blobName = ExtractBlobName(template.FileDocxUrl);
        var stream = await _blobClient.DownloadAsync(blobName, ct: cancellationToken);
        if (stream is null) return DocumentFormErrors.FileNotUploaded;
        return new DownloadDocxResult { FileStream = stream, FileName = Path.GetFileName(blobName), ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
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
        return firstSlashIndex >= 0 ? path[(firstSlashIndex + 1)..] : path;
    }
}

// === SAS URL: tra ve URL tam thoi ===

public sealed record GetFormTemplateDocxSasUrlQuery(Guid TemplateId, int ValidForMinutes = 60) : IQuery<string>;

public sealed class GetFormTemplateDocxSasUrlQueryHandler : IQueryHandler<GetFormTemplateDocxSasUrlQuery, string>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public GetFormTemplateDocxSasUrlQueryHandler(IDocumentDbContext dbContext, IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<string>> Handle(GetFormTemplateDocxSasUrlQuery request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (template is null) return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        if (string.IsNullOrWhiteSpace(template.FileDocxUrl)) return DocumentFormErrors.FileNotUploaded;
        var blobName = ExtractBlobName(template.FileDocxUrl);
        var validFor = TimeSpan.FromMinutes(Math.Clamp(request.ValidForMinutes, 5, 480));
        var sasUrl = await _blobClient.GenerateSasUrlAsync(blobName, validFor, ct: cancellationToken);
        return sasUrl;
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
        return firstSlashIndex >= 0 ? path[(firstSlashIndex + 1)..] : path;
    }
}

