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

/// <param name="PublicOnly">
/// DOC-TPL-PUB-01: API công khai — chỉ mẫu chưa xóa, đang hoạt động, có file; mọi trường hợp khác trả cùng 404
/// `document.template_not_found` để không lộ mẫu đã ẩn. Tên file tải về lấy theo tên mẫu.
/// </param>
public sealed record DownloadFormTemplateDocxQuery(Guid TemplateId, bool PublicOnly = false) : IQuery<DownloadDocxResult>;

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
        var template = await _dbContext.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TemplateId
            && (!request.PublicOnly || (t.IsActive && !t.IsDeleted)), cancellationToken);
        if (template is null) return DocumentFormErrors.TemplateNotFound(request.TemplateId);
        if (string.IsNullOrWhiteSpace(template.FileDocxUrl))
            return request.PublicOnly ? DocumentFormErrors.TemplateNotFound(request.TemplateId) : DocumentFormErrors.FileNotUploaded;
        var blobName = ExtractBlobName(template.FileDocxUrl);
        var stream = await _blobClient.DownloadAsync(blobName, ct: cancellationToken);
        if (stream is null)
            return request.PublicOnly ? DocumentFormErrors.TemplateNotFound(request.TemplateId) : DocumentFormErrors.FileNotUploaded;
        var fileName = request.PublicOnly ? PublicFileName(template.Title, template.Code) : Path.GetFileName(blobName);
        return new DownloadDocxResult { FileStream = stream, FileName = fileName, ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
    }

    /// <summary>Tên file dễ đọc cho người dân: tên mẫu (hoặc mã), bỏ ký tự không hợp lệ, tối đa 150 ký tự, đuôi .docx.</summary>
    public static string PublicFileName(string? title, string? code)
    {
        var name = string.IsNullOrWhiteSpace(title) ? code : title;
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((name ?? "mau-don").Select(c => invalid.Contains(c) || char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length == 0) cleaned = "mau-don";
        if (cleaned.Length > 150) cleaned = cleaned[..150].TrimEnd();
        return cleaned + ".docx";
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

