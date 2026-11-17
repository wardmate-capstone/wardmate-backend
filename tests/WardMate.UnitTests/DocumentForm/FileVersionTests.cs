using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.FileVersions;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Infrastructure.Persistence;
using WardMate.SharedKernel.Blob;

namespace WardMate.UnitTests.DocumentForm;

// DOC-FILEVER-01: chụp tệp theo lần gửi, liệt kê/tải bản cũ, so sánh tờ khai theo ô điền hoặc theo đoạn văn.
public sealed class FileVersionTests
{
    private sealed class MemoryBlobs : IBlobStorageClient
    {
        public readonly Dictionary<string, byte[]> Files = [];
        public int Downloads;
        public Task<string> UploadAsync(Stream stream, string blobName, string contentType, string? containerName = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Stream?> DownloadAsync(string blobName, string? containerName = null, CancellationToken ct = default)
        {
            Downloads++;
            return Task.FromResult<Stream?>(Files.TryGetValue(blobName, out var b) ? new MemoryStream(b) : null);
        }
        public Task DeleteAsync(string blobName, string? containerName = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string blobName, string? containerName = null, CancellationToken ct = default) => Task.FromResult(Files.ContainsKey(blobName));
        public Task<string> GenerateSasUrlAsync(string blobName, TimeSpan validFor, string? containerName = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static readonly Guid App = Guid.NewGuid(), Checklist = Guid.NewGuid();
    private static string Url(string name) => $"https://blob.test/wardmate-files/{name}";

    private static byte[] Docx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(paragraphs.Select(p => new Paragraph(new Run(new Text(p) { Space = SpaceProcessingModeValues.Preserve })))));
        }
        return stream.ToArray();
    }

    private static (DocumentDbContext Db, MemoryBlobs Blobs, FileVersionHandlers Handlers) Setup()
    {
        var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var blobs = new MemoryBlobs();
        return (db, blobs, new FileVersionHandlers(db, blobs));
    }

    [Fact]
    public async Task Capture_records_each_file_with_sha256_and_reuses_hash_of_unchanged_blob()
    {
        var (db, blobs, handlers) = Setup();
        blobs.Files["docs/cccd.jpg"] = "anh cccd"u8.ToArray();
        db.SupportingDocuments.Add(new SupportingDocument(App, "cccd.jpg", Url("docs/cccd.jpg"), Checklist, 8, "image/jpeg", "citizen"));
        await db.SaveChangesAsync();

        var first = await handlers.Handle(new CaptureFileVersionsCommand(App, 1), default);
        var row = Assert.Single(first.Value);
        Assert.Equal(Convert.ToHexString(SHA256.HashData("anh cccd"u8.ToArray())).ToLowerInvariant(), row.Sha256);
        Assert.Equal(SubmissionFileVersion.SupportingDocument, row.SourceType);

        var downloads = blobs.Downloads;
        var second = await handlers.Handle(new CaptureFileVersionsCommand(App, 2), default);
        Assert.Equal(row.Sha256, Assert.Single(second.Value).Sha256);
        Assert.Equal(downloads, blobs.Downloads); // cùng blob → không tải lại để tính hash

        var again = await handlers.Handle(new CaptureFileVersionsCommand(App, 2), default); // nộp lại thất bại rồi nộp tiếp
        Assert.Single(again.Value);
        Assert.Equal(2, (await handlers.Handle(new ListFileVersionsQuery(App, null), default)).Value.Length);
        Assert.Single((await handlers.Handle(new ListFileVersionsQuery(App, 2), default)).Value);
    }

    [Fact]
    public async Task Download_returns_old_file_only_within_the_application()
    {
        var (db, blobs, handlers) = Setup();
        blobs.Files["docs/a.pdf"] = "v1"u8.ToArray();
        db.SupportingDocuments.Add(new SupportingDocument(App, "a.pdf", Url("docs/a.pdf"), Checklist, 2, "application/pdf", "citizen"));
        await db.SaveChangesAsync();
        var id = Assert.Single((await handlers.Handle(new CaptureFileVersionsCommand(App, 1), default)).Value).Id;

        var ok = await handlers.Handle(new DownloadFileVersionQuery(App, id), default);
        Assert.Equal("a.pdf", ok.Value.FileName);
        var other = await handlers.Handle(new DownloadFileVersionQuery(Guid.NewGuid(), id), default);
        Assert.Equal("document.file_version_not_found", other.Error.Code);
    }

    [Fact]
    public void Field_diff_lists_only_changed_fields_in_workflow_shape()
    {
        var diff = DocxDiff.CompareFields("/docx/x",
            new Dictionary<string, string> { ["hoTen"] = "Đức Anh", ["ngaySinh"] = "01/01/2000", ["bo"] = "x" },
            new Dictionary<string, string> { ["hoTen"] = "Nguyễn Đức Anh", ["ngaySinh"] = "01/01/2000", ["a/b"] = "moi" });
        Assert.Equal(3, diff.Length);
        var name = Assert.Single(diff, d => d.FieldLabel == "hoTen");
        Assert.Equal(("Đức Anh", "Nguyễn Đức Anh", true, true), (name.OldValue, name.NewValue, name.OldExists, name.NewExists));
        Assert.Equal("/docx/x/a~1b", Assert.Single(diff, d => d.FieldLabel == "a/b").FieldKey);
        Assert.False(Assert.Single(diff, d => d.FieldLabel == "bo").NewExists);
        Assert.Empty(name.AssociatedComments);
    }

    [Fact]
    public void Paragraph_diff_pairs_edits_and_reports_added_and_removed()
    {
        var oldLines = new[] { "TỜ KHAI", "Họ tên: Đức Anh", "Ngày sinh: 01/01/2000", "Ghi chú cũ" };
        var newLines = new[] { "TỜ KHAI", "Họ tên: Nguyễn Đức Anh", "Ngày sinh: 01/01/2000", "Cam đoan" };
        var diff = DocxDiff.CompareParagraphs("/docx/x", oldLines, newLines);
        Assert.Equal(2, diff.Length);
        Assert.Equal(("Họ tên: Đức Anh", "Họ tên: Nguyễn Đức Anh"), (diff[0].OldValue, diff[0].NewValue));
        Assert.Equal("Đoạn 2", diff[0].FieldLabel);

        var added = DocxDiff.CompareParagraphs("/docx/x", ["A"], ["A", "B"]);
        Assert.False(Assert.Single(added).OldExists);
        var removed = DocxDiff.CompareParagraphs("/docx/x", ["A", "B"], ["A"]);
        Assert.False(Assert.Single(removed).NewExists);
        Assert.Empty(DocxDiff.CompareParagraphs("/docx/x", oldLines, oldLines));
    }

    [Fact]
    public void Docx_paragraphs_are_read_in_order_with_spaces_collapsed()
    {
        Assert.Equal(["Họ tên: Đức Anh", "Địa chỉ"], DocxDiff.ReadParagraphs(Docx("Họ tên:   Đức Anh", "", "Địa chỉ")));
    }

    [Fact]
    public async Task Docx_diff_uses_paragraphs_when_no_form_data_and_empty_when_same_hash()
    {
        var (db, blobs, handlers) = Setup();
        db.SubmissionFileVersions.AddRange(
            new SubmissionFileVersion { ApplicationId = App, ChecklistId = Checklist, SourceId = Guid.NewGuid(), ApplicationVersionNumber = 1,
                BlobUrl = Url("s/1.docx"), FileName = "mau.docx", Sha256 = "a" },
            new SubmissionFileVersion { ApplicationId = App, ChecklistId = Checklist, SourceId = Guid.NewGuid(), ApplicationVersionNumber = 2,
                BlobUrl = Url("s/2.docx"), FileName = "mau.docx", Sha256 = "b" });
        await db.SaveChangesAsync();
        blobs.Files["s/1.docx"] = Docx("Họ tên: Đức Anh");
        blobs.Files["s/2.docx"] = Docx("Họ tên: Nguyễn Đức Anh");
        var diff = await handlers.Handle(new DiffFileVersionsQuery(App, 1, 2, Checklist), default);
        Assert.Equal("Họ tên: Nguyễn Đức Anh", Assert.Single(diff.Value).NewValue);
        Assert.Equal("document.invalid_version", (await handlers.Handle(new DiffFileVersionsQuery(App, 2, 2, Checklist), default)).Error.Code);
        Assert.Equal("document.file_version_not_found", (await handlers.Handle(new DiffFileVersionsQuery(App, 1, 2, Guid.NewGuid()), default)).Error.Code);
    }
}
