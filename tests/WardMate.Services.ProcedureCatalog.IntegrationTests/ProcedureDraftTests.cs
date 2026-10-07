using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ProcedureCatalog.Application.Drafts;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Infrastructure.Drafts;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class ProcedureDraftTests(ProcedureFixture fixture) : IClassFixture<ProcedureFixture>
{
    private const string Route = "/api/v1/procedure-manager/drafts";
    private static MultipartFormDataContent File(string name = "thủ tục.pdf", string bytes = "%PDF-1.7\nTest fixture")
    {
        var data = new MultipartFormDataContent();
        data.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(bytes)), "file", name); return data;
    }
    private static JsonElement Payload() => JsonSerializer.SerializeToElement(new ReviewedProcedureInput
    {
        CategoryId = 1, ProcedureCode = "PDF-" + Guid.NewGuid().ToString("N"), Title = "Thủ tục nhập từ PDF đã đối soát",
        FeeSummary = "Theo văn bản", ProcessingTimeSummary = "Theo văn bản",
        ContentPayload = new() { Cases = Enumerable.Range(1, 10).Select(i => new Domain.JsonModels.ProcedureCaseDetail
            { CaseCode = $"C{i}", CaseName = $"Trường hợp {i}", Steps = [new() { StepOrder = 1, StepName = "Tiếp nhận" }] }).ToList() }
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private async Task<DraftDto> Upload(HttpClient client)
    {
        using var file = File(); using var response = await client.PostAsync(Route, file);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DraftDto>())!;
    }

    [Fact]
    public async Task UploadEditConfirmPublishesOneVersionAndSourceWithoutSasPersistence()
    {
        using var client = fixture.ManagerClient();
        var draft = await Upload(client);
        Assert.Equal("NeedsReview", draft.Status); Assert.NotEmpty(draft.Warnings.EnumerateArray());
        using var source = await client.GetAsync($"{Route}/{draft.Id}/source");
        Assert.Equal(HttpStatusCode.OK, source.StatusCode); Assert.True(source.Headers.CacheControl!.NoStore);
        var saved = await client.PutAsJsonAsync($"{Route}/{draft.Id}", new SaveDraftInput(draft.Revision, Payload()));
        saved.EnsureSuccessStatusCode(); draft = (await saved.Content.ReadFromJsonAsync<DraftDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(draft.Revision, false))).StatusCode);
        var published = await client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(draft.Revision, true));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var procedure = (await published.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        Assert.Equal(10, procedure.ContentPayload.Cases.Count);
        Assert.DoesNotContain("?", procedure.OriginalPdfUrl!);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(draft.Revision, true))).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        Assert.Single(await db.ProcedureVersions.Where(x => x.ProcedureId == procedure.Id).ToListAsync());
        var stored = await db.ProcedureDrafts.SingleAsync(x => x.Id == draft.Id);
        Assert.Equal("Published", stored.Status); Assert.NotNull(stored.ReviewedBy);
        using var citizen = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await citizen.GetAsync($"/api/v1/procedures/{procedure.Id}/source")).StatusCode);
        (await client.PatchAsJsonAsync($"/api/v1/procedure-manager/procedures/{procedure.Id}/status", new { isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await citizen.GetAsync($"/api/v1/procedures/{procedure.Id}/source")).StatusCode);
    }

    [Fact]
    public async Task StaleEditAndInvalidPublicationPreserveDraftAndDatabase()
    {
        using var client = fixture.ManagerClient(); var draft = await Upload(client);
        var first = await client.PutAsJsonAsync($"{Route}/{draft.Id}", new SaveDraftInput(draft.Revision, JsonSerializer.SerializeToElement(new { title = "Chưa đủ thông tin" })));
        first.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{Route}/{draft.Id}", new SaveDraftInput(draft.Revision, Payload()))).StatusCode);
        var current = (await first.Content.ReadFromJsonAsync<DraftDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(current.Revision, true))).StatusCode);
        Assert.Equal("NeedsReview", (await client.GetFromJsonAsync<DraftDto>($"{Route}/{draft.Id}"))!.Status);
    }

    [Theory]
    [InlineData("bad.txt", "%PDF-1.7")]
    [InlineData("bad.pdf", "not-a-pdf")]
    public async Task InvalidFilesRejected(string name, string bytes)
    {
        using var client = fixture.ManagerClient(); using var content = File(name, bytes);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(Route, content)).StatusCode);
    }

    [Fact]
    public async Task DraftsRequireManagerAndMissingConfigurationIsReported()
    {
        using var anonymous = fixture.Factory.CreateClient(); using var citizen = fixture.ManagerClient("REGISTERED_CITIZEN");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.GetAsync(Route)).StatusCode);
        using var manager = fixture.ManagerClient(); var draft = await Upload(manager);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await manager.PostAsJsonAsync($"{Route}/{draft.Id}/retry", new { draft.Revision })).StatusCode);
        var swagger = await manager.GetStringAsync("/swagger/v1/swagger.json");
        Assert.Contains("/api/v1/procedure-manager/drafts/{id}/publish", swagger);
    }

    [Fact]
    public async Task ExpiredLeaseIsRecoveredAndMissingBlobBecomesFailed()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var d = new Domain.Entities.ProcedureDraft { Id = Guid.NewGuid(), Status = "Processing", LeaseUntil = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, BlobName = "missing-test.pdf" };
        db.ProcedureDrafts.Add(d); await db.SaveChangesAsync();
        Assert.True(await DraftExtractionWorker.ProcessNext(scope.ServiceProvider, default));
        db.ChangeTracker.Clear();
        d = await db.ProcedureDrafts.SingleAsync(x => x.Id == d.Id);
        Assert.Equal("Failed", d.Status); Assert.Equal("draft.extraction_failed", d.FailureCode);
    }

    [Fact]
    public async Task WorkerPersistsExtractionButNeverPublishesWithoutReview()
    {
        using var client = fixture.ManagerClient(); var draft = await Upload(client);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        await db.ProcedureDrafts.Where(x => x.Id == draft.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Queued"));
        var payload = Payload();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(scope.ServiceProvider.GetRequiredService<WardMate.SharedKernel.Blob.IBlobStorageClient>());
        services.AddSingleton<IProcedureExtractor>(new FakeExtractor(payload));
        services.AddLogging();
        using var workerServices = services.BuildServiceProvider();
        Assert.True(await DraftExtractionWorker.ProcessNext(workerServices, default));
        var current = (await client.GetFromJsonAsync<DraftDto>($"{Route}/{draft.Id}"))!;
        Assert.Equal("NeedsReview", current.Status);
        Assert.Equal("Nội dung gốc thử nghiệm", current.ExtractedText);
        Assert.Equal(10, current.Payload.GetProperty("contentPayload").GetProperty("cases").GetArrayLength());
        Assert.Null(current.PublishedProcedureId);
        var code = payload.GetProperty("procedureCode").GetString();
        Assert.False(await db.Procedures.AnyAsync(x => x.ProcedureCode == code));
    }

    [Fact]
    public async Task ConcurrentConfirmationCommitsOnlyOnce()
    {
        using var client = fixture.ManagerClient(); var draft = await Upload(client);
        var save = await client.PutAsJsonAsync($"{Route}/{draft.Id}", new SaveDraftInput(draft.Revision, Payload()));
        draft = (await save.Content.ReadFromJsonAsync<DraftDto>())!;
        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(draft.Revision, true)),
            client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(draft.Revision, true)));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnknownNumericFieldsAreNotReplacedByZeroAtPublication()
    {
        using var client = fixture.ManagerClient(); var draft = await Upload(client);
        var payload = System.Text.Json.Nodes.JsonNode.Parse(Payload().GetRawText())!;
        payload["contentPayload"]!["submissionMethods"] = System.Text.Json.Nodes.JsonNode.Parse("[{\"methodName\":\"Trực tiếp\",\"feeAmount\":null,\"feeUnit\":\"VND\",\"estimatedDays\":null}]");
        var save = await client.PutAsJsonAsync($"{Route}/{draft.Id}", new SaveDraftInput(draft.Revision, JsonSerializer.SerializeToElement(payload)));
        draft = (await save.Content.ReadFromJsonAsync<DraftDto>())!;
        var publish = await client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(draft.Revision, true));
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
        Assert.Contains("feeAmount", await publish.Content.ReadAsStringAsync());
    }

    private sealed class FakeExtractor(JsonElement payload) : IProcedureExtractor
    {
        public Task<ExtractionResult> Extract(Stream pdf, CancellationToken ct) =>
            Task.FromResult(new ExtractionResult(payload, "Nội dung gốc thử nghiệm", ["Cần đối soát bản gốc."]));
    }

    [Fact]
    public async Task FailureMarkingDraftPublishedRollsBackProcedureAndVersion()
    {
        using var client = fixture.ManagerClient(); var draft = await Upload(client);
        var payload = Payload();
        var saved = await client.PutAsJsonAsync($"{Route}/{draft.Id}", new SaveDraftInput(draft.Revision, payload));
        draft = (await saved.Content.ReadFromJsonAsync<DraftDto>())!;
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE procedure_drafts ADD CONSTRAINT test_reject_publication CHECK (status <> 'Published') NOT VALID");
        try
        {
            var result = await client.PostAsJsonAsync($"{Route}/{draft.Id}/publish", new ConfirmDraftInput(draft.Revision, true));
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
            var code = payload.GetProperty("procedureCode").GetString();
            Assert.False(await db.Procedures.AnyAsync(x => x.ProcedureCode == code));
            Assert.Equal("NeedsReview", (await db.ProcedureDrafts.SingleAsync(x => x.Id == draft.Id)).Status);
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE procedure_drafts DROP CONSTRAINT test_reject_publication"); }
    }
}
