using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Models;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Commands;

public sealed record UploadDocxResultDto
{
    public Guid TemplateId { get; init; }
    public string BlobUrl { get; init; } = string.Empty;
    public IReadOnlyList<DocxPlaceholder> ExtractedPlaceholders { get; init; } = [];
    public PlaceholderValidationResult? SchemaMatchResult { get; init; }
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
    private const long MaxDocxSizeBytes = 20 * 1024 * 1024; // 20 MB

    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;
    private readonly IDocxPlaceholderEngine _placeholderEngine;
    private readonly IFormSchemaEngine _schemaEngine;

    public UploadFormTemplateDocxCommandHandler(
        IDocumentDbContext dbContext,
        IBlobStorageClient blobClient,
        IDocxPlaceholderEngine placeholderEngine,
        IFormSchemaEngine schemaEngine)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
        _placeholderEngine = placeholderEngine;
        _schemaEngine = schemaEngine;
    }

    public async Task<Result<UploadDocxResultDto>> Handle(UploadFormTemplateDocxCommand request, CancellationToken cancellationToken)
    {
        if (request.FileSizeBytes <= 0 || request.FileStream.Length == 0)
        {
            return Result<UploadDocxResultDto>.Failure(DocumentFormErrors.EmptyFile);
        }

        if (request.FileSizeBytes > MaxDocxSizeBytes)
        {
            return Result<UploadDocxResultDto>.Failure(DocumentFormErrors.FileTooLarge(MaxDocxSizeBytes));
        }

        var extension = Path.GetExtension(request.FileName);
        if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return Result<UploadDocxResultDto>.Failure(DocumentFormErrors.InvalidDocxFile("Only .docx Word template files are accepted."));
        }

        var template = await _dbContext.FormTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<UploadDocxResultDto>.Failure(DocumentFormErrors.TemplateNotFound(request.TemplateId));
        }

        // 1. Bóc tách placeholders từ luồng DOCX
        using var memoryStream = new MemoryStream();
        await request.FileStream.CopyToAsync(memoryStream, cancellationToken);
        var docxBytes = memoryStream.ToArray();

        var extractResult = _placeholderEngine.ExtractPlaceholders(docxBytes);
        if (!extractResult.IsSuccess)
        {
            return Result<UploadDocxResultDto>.Failure(extractResult.Error);
        }

        // 2. Upload file lên Azure Blob Storage
        using var uploadStream = new MemoryStream(docxBytes);
        var blobPath = $"form-templates/{template.Code.ToLowerInvariant()}/{Guid.NewGuid():N}_{Path.GetFileName(request.FileName)}";
        var blobUrl = await _blobClient.UploadAsync(
            blobPath,
            uploadStream,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            cancellationToken);

        // 3. Cập nhật URL file phôi mẫu vào Template
        template.UpdateDocxUrl(blobUrl, request.UpdatedBy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 4. Nếu template đã có version schema, tiến hành đối chiếu placeholder
        PlaceholderValidationResult? matchResult = null;
        var latestVersion = template.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (latestVersion is not null)
        {
            var parseSchemaResult = _schemaEngine.ParseAndValidateSchema(latestVersion.SchemaDefinition);
            if (parseSchemaResult.IsSuccess)
            {
                matchResult = _placeholderEngine.MatchPlaceholdersWithSchema(extractResult.Value, parseSchemaResult.Value);
            }
        }

        var resultDto = new UploadDocxResultDto
        {
            TemplateId = template.Id,
            BlobUrl = blobUrl,
            ExtractedPlaceholders = extractResult.Value,
            SchemaMatchResult = matchResult
        };

        return Result<UploadDocxResultDto>.Success(resultDto);
    }
}
