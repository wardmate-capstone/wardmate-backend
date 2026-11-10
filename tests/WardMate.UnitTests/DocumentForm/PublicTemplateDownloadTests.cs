using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Infrastructure.Persistence;
using WardMate.SharedKernel.Blob;

namespace WardMate.UnitTests.DocumentForm;

// DOC-TPL-PUB-01 (BE-J): tải phôi mẫu công khai chỉ cho mẫu đang hoạt động, có file.
public sealed class PublicTemplateDownloadTests
{
    private const string BlobUrl = "https://blob.test/form-templates/templates/abc123.docx";

    private static async Task<(DownloadFormTemplateDocxQueryHandler Handler, FormTemplate Active, FormTemplate Hidden, FormTemplate NoFile)> Setup()
    {
        var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var active = new FormTemplate("M25", "Mẫu số 25: Đơn đăng ký biến động", BlobUrl);
        var hidden = new FormTemplate("M26", "Mẫu ẩn", BlobUrl);
        hidden.Deactivate();
        var noFile = new FormTemplate("M27", "Mẫu chưa có file");
        db.FormTemplates.AddRange(active, hidden, noFile);
        await db.SaveChangesAsync();
        return (new DownloadFormTemplateDocxQueryHandler(db, new Blobs()), active, hidden, noFile);
    }

    [Fact]
    public async Task Active_template_downloads_with_readable_name()
    {
        var (handler, active, _, _) = await Setup();
        var result = await handler.Handle(new DownloadFormTemplateDocxQuery(active.Id, PublicOnly: true), default);
        Assert.True(result.IsSuccess);
        Assert.Equal("Mẫu số 25 Đơn đăng ký biến động.docx", result.Value.FileName);
    }

    [Fact]
    public async Task Hidden_missing_or_fileless_templates_are_404()
    {
        var (handler, _, hidden, noFile) = await Setup();
        foreach (var id in new[] { hidden.Id, noFile.Id, Guid.NewGuid() })
        {
            var result = await handler.Handle(new DownloadFormTemplateDocxQuery(id, PublicOnly: true), default);
            Assert.Equal("document.template_not_found", result.Error.Code);
        }
    }

    [Fact]
    public async Task Authenticated_download_still_serves_hidden_template()
    {
        var (handler, _, hidden, _) = await Setup();
        Assert.True((await handler.Handle(new DownloadFormTemplateDocxQuery(hidden.Id), default)).IsSuccess);
    }

    [Theory]
    [InlineData("Mẫu: 01/LPTB", "X", "Mẫu 01 LPTB.docx")]
    [InlineData("  ", "M25", "M25.docx")]
    [InlineData(null, null, "mau-don.docx")]
    public void Public_file_name_is_sanitized(string? title, string? code, string expected) =>
        Assert.Equal(expected, DownloadFormTemplateDocxQueryHandler.PublicFileName(title, code));

    private sealed class Blobs : IBlobStorageClient
    {
        public Task<Stream?> DownloadAsync(string blobName, string? containerName = null, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));
        public Task<string> UploadAsync(Stream stream, string blobName, string contentType, string? containerName = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string blobName, string? containerName = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string blobName, string? containerName = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GenerateSasUrlAsync(string blobName, TimeSpan validFor, string? containerName = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
