using System.Security.Cryptography;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.FileVersions;

// DOC-FILEVER-01: lịch sử tệp theo lần gửi hồ sơ, cho cán bộ so sánh giữa các lần gửi.
public sealed record FileVersionDto(Guid Id, Guid? ChecklistId, string SourceType, Guid SourceId, int ApplicationVersionNumber,
    string FileName, long FileSizeBytes, string ContentType, string Sha256, DateTime SourceUpdatedAtUtc, DateTime CreatedAtUtc);
// Cùng dạng FieldDiffDto của Workflow (/diff) để FE dùng lại giao diện; AssociatedComments luôn rỗng ở đây.
public sealed record FileFieldDiffDto(string FieldKey, string FieldLabel, string? OldValue, string? NewValue, bool HasChanged,
    object[] AssociatedComments, bool OldExists, bool NewExists);
public sealed record FileVersionDownload(Stream Content, string FileName, string ContentType);

public sealed record CaptureFileVersionsCommand(Guid ApplicationId, int VersionNumber) : ICommand<FileVersionDto[]>;
public sealed record ListFileVersionsQuery(Guid ApplicationId, int? VersionNumber) : IQuery<FileVersionDto[]>;
public sealed record DownloadFileVersionQuery(Guid ApplicationId, Guid FileVersionId) : IQuery<FileVersionDownload>;
public sealed record DiffFileVersionsQuery(Guid ApplicationId, int FromVersion, int ToVersion, Guid ChecklistId) : IQuery<FileFieldDiffDto[]>;

public sealed class FileVersionHandlers(IDocumentDbContext db, IBlobStorageClient blobs) :
    ICommandHandler<CaptureFileVersionsCommand, FileVersionDto[]>,
    IQueryHandler<ListFileVersionsQuery, FileVersionDto[]>,
    IQueryHandler<DownloadFileVersionQuery, FileVersionDownload>,
    IQueryHandler<DiffFileVersionsQuery, FileFieldDiffDto[]>
{
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>
    /// Chụp tệp hiện có của hồ sơ cho lần gửi <c>VersionNumber</c>. Gọi lại cùng số lần gửi (nộp thất bại rồi nộp lại) thì thay bản chụp cũ.
    /// SHA-256 chỉ tính cho blob mới; blob đã chụp ở lần trước (cùng URL) dùng lại giá trị cũ.
    /// </summary>
    public async Task<Result<FileVersionDto[]>> Handle(CaptureFileVersionsCommand request, CancellationToken ct)
    {
        if (request.VersionNumber < 1) return Error.Validation("document.invalid_version", "Số lần gửi phải từ 1.");
        var submissions = await db.UserSubmissions.AsNoTracking()
            .Where(s => s.ApplicationId == request.ApplicationId && !s.IsDeleted).ToListAsync(ct);
        var documents = await db.SupportingDocuments.AsNoTracking()
            .Where(d => d.ApplicationId == request.ApplicationId && !d.IsDeleted).ToListAsync(ct);
        var known = await db.SubmissionFileVersions.AsNoTracking()
            .Where(v => v.ApplicationId == request.ApplicationId && v.ApplicationVersionNumber < request.VersionNumber)
            .Select(v => new { v.BlobUrl, v.Sha256 }).ToListAsync(ct);
        var hashes = known.GroupBy(x => x.BlobUrl).ToDictionary(g => g.Key, g => g.First().Sha256);
        var rows = new List<SubmissionFileVersion>();
        try
        {
            foreach (var s in submissions)
                rows.Add(new SubmissionFileVersion
                {
                    ApplicationId = request.ApplicationId, ChecklistId = s.ChecklistId, SourceType = SubmissionFileVersion.Submission,
                    SourceId = s.Id, ApplicationVersionNumber = request.VersionNumber, BlobUrl = s.BlobUrl, FileName = s.FileName,
                    FileSizeBytes = s.FileSizeBytes, ContentType = Docx, Sha256 = await Hash(s.BlobUrl, hashes, ct),
                    FormData = s.FormData, SourceUpdatedAtUtc = s.UpdatedAtUtc ?? s.CreatedAtUtc
                });
            foreach (var d in documents)
                rows.Add(new SubmissionFileVersion
                {
                    ApplicationId = request.ApplicationId, ChecklistId = d.ChecklistId, SourceType = SubmissionFileVersion.SupportingDocument,
                    SourceId = d.Id, ApplicationVersionNumber = request.VersionNumber, BlobUrl = d.BlobUrl, FileName = d.FileName,
                    FileSizeBytes = d.FileSizeBytes ?? 0, ContentType = string.IsNullOrWhiteSpace(d.ContentType) ? "application/octet-stream" : d.ContentType,
                    Sha256 = await Hash(d.BlobUrl, hashes, ct), SourceUpdatedAtUtc = d.UpdatedAtUtc ?? d.CreatedAtUtc
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return Error.Failure("document.storage_unavailable", "Chưa đọc được tệp trong kho lưu trữ để chụp lần gửi. Vui lòng thử lại.");
        }
        // Thay bản chụp cũ của cùng lần gửi trong một lần lưu (xóa + thêm cùng giao dịch).
        db.SubmissionFileVersions.RemoveRange(await db.SubmissionFileVersions
            .Where(v => v.ApplicationId == request.ApplicationId && v.ApplicationVersionNumber == request.VersionNumber).ToListAsync(ct));
        db.SubmissionFileVersions.AddRange(rows);
        await db.SaveChangesAsync(ct);
        return rows.Select(Dto).ToArray();
    }

    private async Task<string> Hash(string blobUrl, Dictionary<string, string> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(blobUrl, out var known)) return known;
        await using var stream = await blobs.DownloadAsync(OnlineFormSupport.BlobName(blobUrl), ct: ct);
        // Tệp đã mất khỏi kho vẫn được ghi để cán bộ thấy; hash rỗng nghĩa là không xác định.
        var hash = stream is null ? "" : Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        cache[blobUrl] = hash;
        return hash;
    }

    public async Task<Result<FileVersionDto[]>> Handle(ListFileVersionsQuery request, CancellationToken ct)
    {
        var q = db.SubmissionFileVersions.AsNoTracking().Where(v => v.ApplicationId == request.ApplicationId);
        if (request.VersionNumber is { } n) q = q.Where(v => v.ApplicationVersionNumber == n);
        var rows = await q.OrderBy(v => v.ApplicationVersionNumber).ThenBy(v => v.SourceType).ThenBy(v => v.SourceUpdatedAtUtc).ThenBy(v => v.Id).ToListAsync(ct);
        return rows.Select(Dto).ToArray();
    }

    public async Task<Result<FileVersionDownload>> Handle(DownloadFileVersionQuery request, CancellationToken ct)
    {
        var row = await db.SubmissionFileVersions.AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == request.FileVersionId && v.ApplicationId == request.ApplicationId, ct);
        if (row is null) return NotFound();
        var stream = await blobs.DownloadAsync(OnlineFormSupport.BlobName(row.BlobUrl), ct: ct);
        return stream is null ? NotFound() : new FileVersionDownload(stream, row.FileName, row.ContentType);
    }

    public async Task<Result<FileFieldDiffDto[]>> Handle(DiffFileVersionsQuery request, CancellationToken ct)
    {
        if (request.FromVersion < 1 || request.ToVersion < 1 || request.FromVersion == request.ToVersion)
            return Error.Validation("document.invalid_version", "fromVersion và toVersion phải từ 1 và khác nhau.");
        var rows = await db.SubmissionFileVersions.AsNoTracking()
            .Where(v => v.ApplicationId == request.ApplicationId && v.ChecklistId == request.ChecklistId && v.SourceType == SubmissionFileVersion.Submission
                && (v.ApplicationVersionNumber == request.FromVersion || v.ApplicationVersionNumber == request.ToVersion)).ToListAsync(ct);
        var from = rows.FirstOrDefault(v => v.ApplicationVersionNumber == request.FromVersion);
        var to = rows.FirstOrDefault(v => v.ApplicationVersionNumber == request.ToVersion);
        if (from is null && to is null)
            return Error.NotFound("document.file_version_not_found", "Mục này không có tờ khai ở cả hai lần gửi.");
        var prefix = $"/docx/{request.ChecklistId:D}";
        if (from is not null && to is not null && from.Sha256.Length > 0 && from.Sha256 == to.Sha256) return Array.Empty<FileFieldDiffDto>();
        // Tờ khai trực tuyến: so từng ô điền. Ngược lại so theo đoạn văn của DOCX.
        if (from?.FormData is not null && to?.FormData is not null)
            return DocxDiff.CompareFields(prefix, OnlineFormSupport.Values(JsonDocument.Parse(from.FormData).RootElement),
                OnlineFormSupport.Values(JsonDocument.Parse(to.FormData).RootElement));
        try
        {
            var oldLines = from is null ? Array.Empty<string>() : await Paragraphs(from.BlobUrl, ct);
            var newLines = to is null ? Array.Empty<string>() : await Paragraphs(to.BlobUrl, ct);
            return DocxDiff.CompareParagraphs(prefix, oldLines, newLines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return Error.Failure("document.docx_unreadable", "Không đọc được nội dung tờ khai để so sánh. Mở hai bản để đối chiếu thủ công.");
        }
    }

    private async Task<string[]> Paragraphs(string blobUrl, CancellationToken ct)
    {
        await using var stream = await blobs.DownloadAsync(OnlineFormSupport.BlobName(blobUrl), ct: ct)
            ?? throw new FileNotFoundException("Blob không còn trong kho.");
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return DocxDiff.ReadParagraphs(buffer.ToArray());
    }

    private static Error NotFound() => Error.NotFound("document.file_version_not_found", "Không tìm thấy tệp của lần gửi này.");

    private static FileVersionDto Dto(SubmissionFileVersion v) => new(v.Id, v.ChecklistId, v.SourceType, v.SourceId, v.ApplicationVersionNumber,
        v.FileName, v.FileSizeBytes, v.ContentType, v.Sha256, v.SourceUpdatedAtUtc, v.CreatedAtUtc);
}

/// <summary>So sánh nội dung tờ khai: theo ô điền (tờ khai trực tuyến) hoặc theo đoạn văn (DOCX).</summary>
public static class DocxDiff
{
    public const int MaxParagraphs = 2000;

    public static FileFieldDiffDto[] CompareFields(string prefix, IReadOnlyDictionary<string, string> oldValues, IReadOnlyDictionary<string, string> newValues) =>
        oldValues.Keys.Union(newValues.Keys).Order(StringComparer.Ordinal).Select(key =>
        {
            var oldExists = oldValues.TryGetValue(key, out var o);
            var newExists = newValues.TryGetValue(key, out var n);
            return new FileFieldDiffDto($"{prefix}/{Escape(key)}", key, o, n, oldExists != newExists || o != n, [], oldExists, newExists);
        }).Where(x => x.HasChanged).ToArray();

    /// <summary>Đoạn văn khác rỗng (gồm cả ô bảng), theo thứ tự trong tài liệu; khoảng trắng thừa được gộp.</summary>
    public static string[] ReadParagraphs(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null) return [];
        return body.Descendants<Paragraph>()
            .Select(p => string.Join(' ', p.InnerText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(t => t.Length > 0).Take(MaxParagraphs).ToArray();
    }

    /// <summary>
    /// So theo đoạn bằng dãy con chung dài nhất. Một đoạn bị xóa đi liền một đoạn được thêm ở cùng chỗ thì coi là "sửa" (một dòng cũ → mới).
    /// FieldLabel ghi số thứ tự đoạn trong bản mới (hoặc bản cũ nếu đoạn bị xóa).
    /// </summary>
    public static FileFieldDiffDto[] CompareParagraphs(string prefix, IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        int n = oldLines.Count, m = newLines.Count;
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                lcs[i, j] = oldLines[i] == newLines[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
        var result = new List<FileFieldDiffDto>();
        var removed = new List<int>(); var added = new List<int>();
        void Flush()
        {
            var pairs = Math.Min(removed.Count, added.Count);
            for (var k = 0; k < pairs; k++)
                result.Add(new($"{prefix}/p/{added[k] + 1}", $"Đoạn {added[k] + 1}", oldLines[removed[k]], newLines[added[k]], true, [], true, true));
            foreach (var r in removed.Skip(pairs))
                result.Add(new($"{prefix}/p-old/{r + 1}", $"Đoạn {r + 1} (bản cũ, đã xóa)", oldLines[r], null, true, [], true, false));
            foreach (var a in added.Skip(pairs))
                result.Add(new($"{prefix}/p/{a + 1}", $"Đoạn {a + 1} (thêm mới)", null, newLines[a], true, [], false, true));
            removed.Clear(); added.Clear();
        }
        int x = 0, y = 0;
        while (x < n || y < m)
        {
            if (x < n && y < m && oldLines[x] == newLines[y]) { Flush(); x++; y++; }
            else if (y < m && (x == n || lcs[x, y + 1] >= lcs[x + 1, y])) added.Add(y++);
            else removed.Add(x++);
        }
        Flush();
        return result.ToArray();
    }

    // RFC 6901: "~" → "~0", "/" → "~1", như khóa của /diff bên Workflow.
    private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
}
