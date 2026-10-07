using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Drafts;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using WardMate.SharedKernel.Blob;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class AdministrationTests(ProcedureFixture fixture) : IClassFixture<ProcedureFixture>
{
    private const string Root = "/api/v1/procedure-manager";
    private static ProcedureInput Input(int category = 1) => new()
    {
        CategoryId = category, ProcedureCode = "ADMIN-" + Guid.NewGuid().ToString("N"),
        Title = "Thủ tục kiểm tra quản trị mới", ContentPayload = new() { DecisionNumber = "QD-OLD" }
    };
    private static async Task<DraftDto> Upload(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7\nSynthetic test")), "file", "test.pdf");
        var result = await client.PostAsync(Root + "/drafts", form);
        result.EnsureSuccessStatusCode(); return (await result.Content.ReadFromJsonAsync<DraftDto>())!;
    }
    [Fact]
    public async Task ManagerCanReadInactiveButPublicCannot()
    {
        using var client = fixture.ManagerClient();
        var created = await client.PostAsJsonAsync(Root + "/procedures", Input()); created.EnsureSuccessStatusCode();
        var p = (await created.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        (await client.PatchAsJsonAsync($"{Root}/procedures/{p.Id}/status", new { isActive = false })).EnsureSuccessStatusCode();
        var detail = await client.GetFromJsonAsync<ProcedureDetailDto>($"{Root}/procedures/{p.Id}");
        Assert.False(detail!.IsActive);
        using var citizen = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await citizen.GetAsync($"/api/v1/procedures/{p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Root}/procedures/{Guid.NewGuid()}")).StatusCode);
    }
    [Fact]
    public async Task CategoryCrudRejectsDuplicateInvalidAndLinkedDelete()
    {
        using var client = fixture.ManagerClient();
        var name = "Lĩnh vực " + Guid.NewGuid();
        var create = await client.PostAsJsonAsync(Root + "/categories", new CategoryInput(name, "Mô tả"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var category = (await create.Content.ReadFromJsonAsync<ProcedureCategoryDto>())!;
        Assert.True(category.Id > 3);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Root + "/categories", new CategoryInput(name.ToUpperInvariant(), null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Root + "/categories", new CategoryInput(" ", null))).StatusCode);
        (await client.PutAsJsonAsync($"{Root}/categories/{category.Id}", new CategoryInput(name + " sửa", null))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Root}/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Root}/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync(Root + "/categories/1")).StatusCode);
    }
    [Fact]
    public async Task RollbackPreservesPreviousStateAuditAndInactiveStatus()
    {
        using var client = fixture.ManagerClient(); var input = Input();
        var create = await client.PostAsJsonAsync(Root + "/procedures", input); create.EnsureSuccessStatusCode();
        var p = (await create.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        var update = new UpdateProcedureInput { CategoryId = 1, ProcedureCode = input.ProcedureCode,
            Title = "Nội dung thay đổi trước khôi phục", DecisionNumber = "QD-NEW", EffectiveDate = new(2026, 10, 6) };
        (await client.PutAsJsonAsync($"{Root}/procedures/{p.Id}", update)).EnsureSuccessStatusCode();
        (await client.PatchAsJsonAsync($"{Root}/procedures/{p.Id}/status", new { isActive = false })).EnsureSuccessStatusCode();
        var restored = await client.PostAsJsonAsync($"{Root}/procedures/{p.Id}/versions/1/rollback",
            new RollbackInput("Sửa nội dung nhầm", "QD-RESTORE", new(2026, 10, 6)));
        restored.EnsureSuccessStatusCode();
        var result = (await restored.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        Assert.Equal(input.Title, result.Title); Assert.False(result.IsActive);
        Assert.Equal("QD-RESTORE", result.ContentPayload.DecisionNumber);
        var versions = await client.GetFromJsonAsync<ProcedureVersionDto[]>($"{Root}/procedures/{p.Id}/versions");
        Assert.Equal(3, versions!.Length);
        Assert.Equal(update.Title, versions[0].SnapshotData.GetProperty("title").GetString());
        Assert.Equal(1, versions[0].SnapshotData.GetProperty("rollback").GetProperty("restoredFromVersion").GetInt32());
        Assert.Equal("Sửa nội dung nhầm", versions[0].SnapshotData.GetProperty("rollback").GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Root}/procedures/{p.Id}/versions/999/rollback",
            new RollbackInput("Không tồn tại", "QD", new(2026, 10, 6)))).StatusCode);
    }
    [Fact]
    public async Task DiscardRemovesBlobAndRowAndBlocksWorkerAndPublishedDraft()
    {
        using var client = fixture.ManagerClient(); var d = await Upload(client);
        var blobs = fixture.Factory.Services.GetRequiredService<IBlobStorageClient>();
        var blobName = $"drafts/{d.Id}/source.pdf";
        Assert.True(await blobs.ExistsAsync(blobName));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Root}/drafts/{d.Id}")).StatusCode);
        Assert.False(await blobs.ExistsAsync(blobName));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Root}/drafts/{d.Id}")).StatusCode);
        foreach (var status in new[] { "Processing", "Published" })
        {
            var blocked = await Upload(client);
            using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
            await db.ProcedureDrafts.Where(x => x.Id == blocked.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"{Root}/drafts/{blocked.Id}")).StatusCode);
            Assert.True(await blobs.ExistsAsync($"drafts/{blocked.Id}/source.pdf"));
        }
    }
    [Fact]
    public async Task VersionSourceSignsHistoricalBlobAndRejectsUnrelatedVersion()
    {
        using var client = fixture.ManagerClient(); var d = await Upload(client);
        var input = Input();
        input.OriginalPdfUrl = $"https://storage.example.test/procedure-sources/drafts/{d.Id}/source.pdf";
        input.PdfFileName = "test.pdf";
        var create = await client.PostAsJsonAsync(Root + "/procedures", input); create.EnsureSuccessStatusCode();
        var p = (await create.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        await db.ProcedureDrafts.Where(x => x.Id == d.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Published").SetProperty(x => x.PublishedProcedureId, p.Id));
        var version = await db.ProcedureVersions.SingleAsync(x => x.ProcedureId == p.Id);
        await db.Procedures.Where(x => x.Id == p.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.OriginalPdfUrl, "https://example.test/new.pdf").SetProperty(x => x.IsActive, false));
        var response = await client.GetAsync($"{Root}/procedures/{p.Id}/versions/{version.Id}/source");
        response.EnsureSuccessStatusCode(); Assert.True(response.Headers.CacheControl!.NoStore);
        var link = (await response.Content.ReadFromJsonAsync<SourceLinkDto>())!;
        Assert.Contains(d.Id.ToString(), link.Url); Assert.Equal(600, link.ExpiresInSeconds);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Root}/procedures/{Guid.NewGuid()}/versions/{version.Id}/source")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"{Root}/drafts/{d.Id}")).StatusCode);
    }
    [Theory]
    [InlineData("REGISTERED_CITIZEN", HttpStatusCode.Forbidden)]
    [InlineData("MANAGER", HttpStatusCode.Forbidden)]
    public async Task NewEndpointsRequireProcedureManager(string role, HttpStatusCode expected)
    {
        using var client = fixture.ManagerClient(role);
        Assert.Equal(expected, (await client.GetAsync($"{Root}/procedures/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync(Root + "/categories", new CategoryInput("X", null))).StatusCode);
        Assert.Equal(expected, (await client.DeleteAsync($"{Root}/drafts/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync(Root + "/document-forms")).StatusCode);
    }

    [Fact]
    public async Task FailedBlobDeleteRetainsIntentAndCanBeRetried()
    {
        using var client = fixture.ManagerClient(); var draft = await Upload(client);
        var blobName = $"drafts/{draft.Id}/source.pdf";
        var storage = (DraftTestBlobStorage)fixture.Factory.Services.GetRequiredService<IBlobStorageClient>();
        storage.DeleteFailures[blobName] = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.DeleteAsync($"{Root}/drafts/{draft.Id}")).StatusCode);
        var pending = await client.GetFromJsonAsync<DraftDto>($"{Root}/drafts/{draft.Id}");
        Assert.Equal("Deleting", pending!.Status); Assert.True(await storage.ExistsAsync(blobName));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{Root}/drafts/{draft.Id}",
            new SaveDraftInput(pending.Revision, JsonSerializer.SerializeToElement(new { title = "Không cho sửa" })))).StatusCode);
        storage.DeleteFailures.TryRemove(blobName, out _);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Root}/drafts/{draft.Id}")).StatusCode);
        Assert.False(await storage.ExistsAsync(blobName));
    }

    [Fact]
    public async Task ConcurrentCategoryCreateHasOneWinner()
    {
        using var client = fixture.ManagerClient(); var input = new CategoryInput("Đồng thời " + Guid.NewGuid(), null);
        var results = await Task.WhenAll(client.PostAsJsonAsync(Root + "/categories", input), client.PostAsJsonAsync(Root + "/categories", input));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ConcurrentRollbackAllocatesDistinctVersions()
    {
        using var client = fixture.ManagerClient("IT_ADMIN");
        var create = await client.PostAsJsonAsync(Root + "/procedures", Input()); create.EnsureSuccessStatusCode();
        var p = (await create.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        var url = $"{Root}/procedures/{p.Id}/versions/1/rollback";
        var input = new RollbackInput("Khôi phục đồng thời", "QD", new(2026, 10, 6));
        var results = await Task.WhenAll(client.PostAsJsonAsync(url, input), client.PostAsJsonAsync(url, input));
        Assert.All(results, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        var versions = await client.GetFromJsonAsync<ProcedureVersionDto[]>($"{Root}/procedures/{p.Id}/versions");
        Assert.Equal(new[] { 3, 2, 1 }, versions!.Select(x => x.VersionNumber));
    }

    [Fact]
    public async Task RollbackRejectsInvalidSnapshotWithoutCreatingVersion()
    {
        using var client = fixture.ManagerClient();
        var create = await client.PostAsJsonAsync(Root + "/procedures", Input()); create.EnsureSuccessStatusCode();
        var p = (await create.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        await db.ProcedureVersions.Where(x => x.ProcedureId == p.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SnapshotData, "{}"));
        var result = await client.PostAsJsonAsync($"{Root}/procedures/{p.Id}/versions/1/rollback", new RollbackInput("Sai dữ liệu", "QD", new(2026, 10, 6)));
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal(1, await db.ProcedureVersions.CountAsync(x => x.ProcedureId == p.Id));
    }
}
