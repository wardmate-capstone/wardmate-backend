using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record UploadDocxResultDto
{
    public Guid TemplateId { get; init; }
    public string BlobUrl { get; init; } = string.Empty;
}

public sealed record UploadFormTemplateDocxCommand(
    Guid TemplateId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? UpdatedBy = null) : ICommand<UploadDocxResultDto>;

public sealed class UploadFormTemplateDocxCommandHandler : ICommandHandler<UploadFormTemplateDocxCommand, UploadDocxResultDto>
{

    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public UploadFormTemplateDocxCommandHandler(
        IDocumentDbContext dbContext,
        IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<UploadDocxResultDto>> Handle(UploadFormTemplateDocxCommand request, CancellationToken cancellationToken)
    {
        var file = await WardMate.Services.DocumentForm.Application.Services.DocxUpload.ReadAsync(
            request.FileStream, request.FileName, request.FileSizeBytes, cancellationToken);
        if (!file.IsSuccess) return file.Error;
        var template = await _dbContext.FormTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        }

        var docxBytes = file.Value;
        using var uploadStream = new MemoryStream(docxBytes);
        var blobPath = $"form-templates/{template.Code.ToLowerInvariant()}/{Guid.NewGuid():N}_{Path.GetFileName(request.FileName)}";
        var blobUrl = await _blobClient.UploadAsync(
            uploadStream,
            blobPath,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ct: cancellationToken);

        template.UpdateDocxUrl(blobUrl, request.UpdatedBy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var resultDto = new UploadDocxResultDto
        {
            TemplateId = template.Id,
            BlobUrl = blobUrl
        };

        return resultDto;
    }
}
