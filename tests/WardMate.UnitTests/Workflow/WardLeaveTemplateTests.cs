using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using WardMate.SharedKernel.Blob;

namespace WardMate.UnitTests.Workflow;

public sealed class WardLeaveTemplateTests
{
    private sealed class MemoryBlobClient : IBlobStorageClient
    {
        public readonly Dictionary<string, byte[]> Storage = new();

        public Task<string> UploadAsync(Stream content, string blobName, string contentType, string? containerName = null, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            Storage[blobName] = ms.ToArray();
            return Task.FromResult($"https://fake.blob/{blobName}");
        }

        public Task<Stream?> DownloadAsync(string blobName, string? containerName = null, CancellationToken ct = default)
        {
            if (Storage.TryGetValue(blobName, out var bytes))
            {
                return Task.FromResult<Stream?>(new MemoryStream(bytes));
            }
            return Task.FromResult<Stream?>(null);
        }

        public Task DeleteAsync(string blobName, string? containerName = null, CancellationToken ct = default)
        {
            Storage.Remove(blobName);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string blobName, string? containerName = null, CancellationToken ct = default)
        {
            return Task.FromResult(Storage.ContainsKey(blobName));
        }

        public Task<string> GenerateSasUrlAsync(string blobName, TimeSpan validFor, string? containerName = null, CancellationToken ct = default)
            => Task.FromResult($"https://fake.blob/{blobName}?sas=true");
    }

    private static byte[] CreateValidDocx()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var contentTypes = zip.CreateEntry("[Content_Types].xml");
            using (var stream = contentTypes.Open())
            {
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>";
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }

            var rels = zip.CreateEntry("_rels/.rels");
            using (var stream = rels.Open())
            {
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>";
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }

            var doc = zip.CreateEntry("word/document.xml");
            using (var stream = doc.Open())
            {
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Đơn xin nghỉ</w:t></w:r></w:p></w:body></w:document>";
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }
        }
        return ms.ToArray();
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new WorkflowDbContext(options);
    }

    private static ReviewActor ManagerActor(string ward = "W01") =>
        new(Guid.NewGuid(), IsOfficer: true, WardCode: ward, IsManager: true, FullName: "Nguyễn Văn Manager");

    private static ReviewActor OfficerActor(string ward = "W01") =>
        new(Guid.NewGuid(), IsOfficer: true, WardCode: ward, IsManager: false, FullName: "Trần Cán Bộ");

    [Fact]
    public async Task GetCurrent_WhenDbTemplateExists_ReturnsDbTemplate()
    {
        using var db = CreateDbContext();
        var templateId = Guid.NewGuid();
        var templateHash = new string('A', 64);
        db.WardLeaveTemplates.Add(new WardLeaveTemplate
        {
            Id = templateId,
            WardCode = "W01",
            Sha256 = templateHash,
            SizeBytes = 1024,
            FileName = "don-nghi-w01.docx",
            UploadedBy = Guid.NewGuid(),
            UploadedByName = "Manager A",
            UploadedAtUtc = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System);

        var result = await service.Template(OfficerActor("W01"), default);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(templateId, result.Value.VersionId);
        Assert.Equal(templateHash, result.Value.Sha256);
    }

    [Fact]
    public async Task GetCurrent_WhenNoDbTemplate_FallsBackToConfig()
    {
        using var db = CreateDbContext();
        var configTemplateId = Guid.NewGuid();
        var configHash = new string('F', 64);
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["WorkSchedule:SharedLeaveTemplate:TemplateVersionId"] = configTemplateId.ToString(),
            ["WorkSchedule:SharedLeaveTemplate:TemplateSha256"] = configHash,
            ["WorkSchedule:SharedLeaveTemplate:RequireWord"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System);

        var result = await service.Template(OfficerActor("W01"), default);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(configTemplateId, result.Value.VersionId);
        Assert.Equal(configHash, result.Value.Sha256);
    }

    [Fact]
    public async Task GetCurrent_WhenNoDbAndNoConfig_Returns503()
    {
        using var db = CreateDbContext();
        var config = new ConfigurationBuilder().Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System);

        var result = await service.Template(OfficerActor("W01"), default);

        Assert.NotNull(result.Error);
        Assert.Equal(503, result.Error.Status);
        Assert.Equal("leave.template_unavailable", result.Error.Code);
    }

    [Fact]
    public async Task UploadTemplate_ByManager_SucceedsAndSetsActive()
    {
        using var db = CreateDbContext();
        var blobClient = new MemoryBlobClient();
        var config = new ConfigurationBuilder().Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System, blobClient);

        var docxBytes = CreateValidDocx();
        using var stream = new MemoryStream(docxBytes);

        var result = await service.UploadTemplate(ManagerActor("W01"), stream, "mau-moi.docx", default);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal("W01", result.Value.WardCode);
        Assert.Equal("mau-moi.docx", result.Value.FileName);
        Assert.True(result.Value.IsActive);
        Assert.Equal(docxBytes.Length, result.Value.SizeBytes);

        // Verify blob uploaded
        var key = $"templates/{result.Value.Id:N}.docx";
        Assert.True(blobClient.Storage.ContainsKey(key));

        // Verify DB saved
        var dbRecord = await db.WardLeaveTemplates.SingleOrDefaultAsync(x => x.Id == result.Value.Id);
        Assert.NotNull(dbRecord);
        Assert.True(dbRecord.IsActive);
    }

    [Fact]
    public async Task UploadTemplate_WhenUploadingSecondTime_DeactivatesPreviousTemplate()
    {
        using var db = CreateDbContext();
        var blobClient = new MemoryBlobClient();
        var config = new ConfigurationBuilder().Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System, blobClient);

        var docx1 = CreateValidDocx();
        using (var s1 = new MemoryStream(docx1))
        {
            var r1 = await service.UploadTemplate(ManagerActor("W01"), s1, "mau-1.docx", default);
            Assert.Null(r1.Error);
        }

        var docx2 = CreateValidDocx();
        Guid secondId;
        using (var s2 = new MemoryStream(docx2))
        {
            var r2 = await service.UploadTemplate(ManagerActor("W01"), s2, "mau-2.docx", default);
            Assert.Null(r2.Error);
            secondId = r2.Value!.Id;
        }

        var allTemplates = await db.WardLeaveTemplates.Where(x => x.WardCode == "W01").ToListAsync();
        Assert.Equal(2, allTemplates.Count);

        var activeTemplates = allTemplates.Where(x => x.IsActive).ToList();
        var active = Assert.Single(activeTemplates);
        Assert.Equal(secondId, active.Id);
        Assert.Equal("mau-2.docx", active.FileName);
    }

    [Fact]
    public async Task UploadTemplate_ByOfficer_Returns403Forbidden()
    {
        using var db = CreateDbContext();
        var blobClient = new MemoryBlobClient();
        var config = new ConfigurationBuilder().Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System, blobClient);

        var docxBytes = CreateValidDocx();
        using var stream = new MemoryStream(docxBytes);

        var result = await service.UploadTemplate(OfficerActor("W01"), stream, "mau.docx", default);

        Assert.NotNull(result.Error);
        Assert.Equal(403, result.Error.Status);
        Assert.Equal("leave.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task UploadTemplate_WithNonDocxExtension_Returns415()
    {
        using var db = CreateDbContext();
        var blobClient = new MemoryBlobClient();
        var config = new ConfigurationBuilder().Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System, blobClient);

        var docxBytes = CreateValidDocx();
        using var stream = new MemoryStream(docxBytes);

        var result = await service.UploadTemplate(ManagerActor("W01"), stream, "mau.pdf", default);

        Assert.NotNull(result.Error);
        Assert.Equal(415, result.Error.Status);
        Assert.Equal("leave.invalid_docx", result.Error.Code);
    }

    [Fact]
    public async Task ListTemplates_ReturnsTemplatesForWardOrderedDescending()
    {
        using var db = CreateDbContext();
        var config = new ConfigurationBuilder().Build();
        var service = new LeaveDraftService(db, config, TimeProvider.System);

        db.WardLeaveTemplates.AddRange(
            new WardLeaveTemplate { Id = Guid.NewGuid(), WardCode = "W01", FileName = "v1.docx", Sha256 = "hash1", UploadedByName = "M", UploadedAtUtc = DateTime.UtcNow.AddHours(-2), IsActive = false },
            new WardLeaveTemplate { Id = Guid.NewGuid(), WardCode = "W01", FileName = "v2.docx", Sha256 = "hash2", UploadedByName = "M", UploadedAtUtc = DateTime.UtcNow, IsActive = true },
            new WardLeaveTemplate { Id = Guid.NewGuid(), WardCode = "W02", FileName = "w2.docx", Sha256 = "hash3", UploadedByName = "M", UploadedAtUtc = DateTime.UtcNow, IsActive = true }
        );
        await db.SaveChangesAsync();

        var result = await service.ListTemplates(ManagerActor("W01"), default);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Length);
        Assert.Equal("v2.docx", result.Value[0].FileName);
        Assert.Equal("v1.docx", result.Value[1].FileName);
    }
}
