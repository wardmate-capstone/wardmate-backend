using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class ReviewedProcedureTests(ProcedureFixture fixture) : IClassFixture<ProcedureFixture>
{
    private const string Route = "/api/v1/procedure-manager/procedures";
    private static ReviewedProcedureInput Draft() => new()
    {
        CategoryId = 1, ProcedureCode = "REVIEW-" + Guid.NewGuid().ToString("N"), Title = "Thủ tục đã đối soát thử nghiệm",
        OriginalPdfUrl = "https://wardmatetest.blob.core.windows.net/procedures/review-v1.pdf", PdfFileName = "Bản gốc đã đối soát.pdf",
        FeeSummary = "Theo từng trường hợp", ProcessingTimeSummary = "Theo hình thức nộp",
        ContentPayload = new()
        {
            DecisionNumber = "TEST-QD-01", ReceivingAddress = "Bộ phận tiếp nhận thử nghiệm",
            Cases = Enumerable.Range(1, 10).Select(i => new Domain.JsonModels.ProcedureCaseDetail
            {
                CaseCode = $"CASE-{i}", CaseName = $"Trường hợp {i}", Steps = [new()
                { StepOrder = 1, StepName = "Kiểm tra", Executor = "Cán bộ", ActionDetails = $"Kiểm tra hồ sơ trường hợp {i}\nGiữ nguyên nội dung" }]
            }).ToList(),
            SubmissionMethods = [new() { MethodName = "Trực tiếp", FeeAmount = 15000, EstimatedDays = 3, Note = "Mức phí minh họa" },
                new() { MethodName = "Trực tuyến", FeeAmount = 7000, EstimatedDays = 3 }],
            LegalReferences = [new() { DocumentNumber = "TEST-LEGAL", DocumentName = "Văn bản minh họa", Authority = "Cơ quan mẫu", IssueDate = new(2026, 1, 1) }],
            Results = ["Kết quả xử lý thử nghiệm"]
        },
        ChecklistSchema = Enumerable.Range(1, 10).Select(i => new Domain.JsonModels.ChecklistItemSchema
        { ChecklistId = $"DOC-{i}", CaseCode = $"CASE-{i}", ItemName = $"Giấy tờ {i}", SubmissionType = i % 2 == 0 ? "NOP" : "XUAT_TRINH", Quantity = i }).ToList(),
        FormDefinitions = Enumerable.Range(1, 10).Select(i => new Domain.JsonModels.FormDefinitionSchema
        { FormCode = $"FORM-{i}", FormName = $"Tờ khai {i}", CaseCode = $"CASE-{i}", FormTemplateId = Guid.NewGuid(), FormType = "DOCX_TEMPLATE" }).ToList()
    };

    private static async Task<ProcedureDetailDto> Publish(HttpClient client, ReviewedProcedureInput input)
    {
        using var response = await client.PostAsJsonAsync(Route + "/publish", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
    }

    [Fact]
    public async Task PublishCreatesTenCaseJsonbPdfAndVersionThenUpdatesWithOldSnapshot()
    {
        using var client = fixture.ManagerClient();
        var draft = Draft();
        var first = await Publish(client, draft);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
            var saved = await db.Procedures.SingleAsync(x => x.Id == first.Id);
            Assert.True(saved.IsActive);
            Assert.Equal(draft.OriginalPdfUrl, saved.OriginalPdfUrl);
            Assert.Equal(draft.PdfFileName, saved.PdfFileName);
            Assert.Equal(JsonSerializer.Serialize(draft.ContentPayload), JsonSerializer.Serialize(saved.ContentPayload));
            Assert.Equal(JsonSerializer.Serialize(draft.ChecklistSchema), JsonSerializer.Serialize(saved.ChecklistSchema));
            Assert.Equal(JsonSerializer.Serialize(draft.FormDefinitions), JsonSerializer.Serialize(saved.FormDefinitions));
            var version = await db.ProcedureVersions.SingleAsync(x => x.ProcedureId == first.Id);
            Assert.Equal(1, version.VersionNumber);
            Assert.Equal(first.OriginalPdfUrl, version.OriginalPdfUrl);
            Assert.Equal(first.PdfFileName, version.PdfFileName);
        }
        draft.Title = "Thủ tục đã đối soát phiên bản thứ hai";
        draft.OriginalPdfUrl = draft.OriginalPdfUrl!.Replace("v1.pdf", "v2.pdf");
        draft.PdfFileName = "Bản gốc lần hai.pdf";
        draft.ContentPayload.Cases[9].Steps[0].ActionDetails = "Đã sửa trường hợp thứ mười";
        var updated = await Publish(client, draft);
        Assert.Equal(first.Id, updated.Id);
        Assert.True(Math.Abs((first.CreatedAt - updated.CreatedAt).TotalMilliseconds) < 1);
        Assert.True(updated.UpdatedAt >= first.UpdatedAt);
        var versions = (await client.GetFromJsonAsync<List<ProcedureVersionDto>>($"{Route}/{first.Id}/versions"))!;
        Assert.Equal(new[] { 2, 1 }, versions.Select(x => x.VersionNumber));
        Assert.All(versions, version =>
        {
            Assert.Equal(first.OriginalPdfUrl, version.OriginalPdfUrl);
            Assert.Equal(first.PdfFileName, version.PdfFileName);
            Assert.Equal(first.OriginalPdfUrl, version.SnapshotData.GetProperty("originalPdfUrl").GetString());
            Assert.Equal(10, version.SnapshotData.GetProperty("contentPayload").GetProperty("cases").GetArrayLength());
            Assert.Equal(first.ContentPayload.Cases[9].Steps[0].ActionDetails,
                version.SnapshotData.GetProperty("contentPayload").GetProperty("cases")[9].GetProperty("steps")[0].GetProperty("actionDetails").GetString());
        });
        var publicList = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"/api/v1/procedures?keyword={draft.ProcedureCode}"))!;
        Assert.Equal(draft.OriginalPdfUrl, Assert.Single(publicList.Items).OriginalPdfUrl);
        var managerList = (await client.GetFromJsonAsync<PagedResult<ProcedureManagerSummaryDto>>($"{Route}?keyword={draft.ProcedureCode}"))!;
        var item = Assert.Single(managerList.Items);
        Assert.Equal(2, item.VersionCount);
        Assert.True(item.IsActive);
        Assert.True(Math.Abs((first.CreatedAt - item.CreatedAt).TotalMilliseconds) < 1);
    }

    [Fact]
    public async Task PublishPreservesClosedStatusAndManagerFiltersIt()
    {
        using var client = fixture.ManagerClient();
        var draft = Draft();
        var first = await Publish(client, draft);
        (await client.PatchAsJsonAsync($"{Route}/{first.Id}/status", new { isActive = false, reason = "Đóng thử nghiệm" })).EnsureSuccessStatusCode();
        draft.OriginalPdfUrl = null; draft.PdfFileName = null;
        Assert.False((await Publish(client, draft)).IsActive);
        var publicList = (await client.GetFromJsonAsync<PagedResult<ProcedureSummaryDto>>($"/api/v1/procedures?keyword={draft.ProcedureCode}&isActive=false"))!;
        Assert.Empty(publicList.Items);
        foreach (var filter in new[] { "", "&isActive=false" })
        {
            var list = (await client.GetFromJsonAsync<PagedResult<ProcedureManagerSummaryDto>>($"{Route}?keyword={draft.ProcedureCode}{filter}"))!;
            Assert.False(Assert.Single(list.Items).IsActive);
            Assert.Equal(2, list.Items[0].VersionCount);
        }
        Assert.Empty((await client.GetFromJsonAsync<PagedResult<ProcedureManagerSummaryDto>>($"{Route}?keyword={draft.ProcedureCode}&isActive=true"))!.Items);
    }

    [Fact]
    public async Task ConcurrentPublishOfNewCodeCreatesOneProcedureAndSequentialVersions()
    {
        using var client = fixture.ManagerClient();
        var draft = Draft();
        var results = await Task.WhenAll(Publish(client, draft), Publish(client, draft));
        Assert.Equal(results[0].Id, results[1].Id);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        Assert.Equal(1, await db.Procedures.CountAsync(x => x.ProcedureCode == draft.ProcedureCode));
        Assert.Equal(new[] { 1, 2 }, await db.ProcedureVersions.Where(x => x.ProcedureId == results[0].Id).OrderBy(x => x.VersionNumber).Select(x => x.VersionNumber).ToArrayAsync());
    }

    [Theory]
    [InlineData("category")]
    [InlineData("url")]
    [InlineData("url-length")]
    [InlineData("filename")]
    [InlineData("fee")]
    [InlineData("payload")]
    public async Task InvalidDraftDoesNotModifyProcedureOrAppendVersion(string kind)
    {
        using var client = fixture.ManagerClient();
        var draft = Draft();
        var first = await Publish(client, draft);
        switch (kind)
        {
            case "category": draft.CategoryId = int.MaxValue; break;
            case "url": draft.OriginalPdfUrl = "javascript:alert(1)"; break;
            case "url-length": draft.OriginalPdfUrl = "https://example.test/" + new string('a', 500); break;
            case "filename": draft.PdfFileName = new string('a', 256); break;
            case "fee": draft.FeeSummary = ""; break;
            case "payload": draft.ContentPayload = null!; break;
        }
        using var response = await client.PostAsJsonAsync(Route + "/publish", draft);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        Assert.Equal(first.OriginalPdfUrl, (await db.Procedures.SingleAsync(x => x.Id == first.Id)).OriginalPdfUrl);
        Assert.Equal(1, await db.ProcedureVersions.CountAsync(x => x.ProcedureId == first.Id));
    }

    [Fact]
    public async Task DatabaseFailureRollsBackBothLiveDataAndSnapshot()
    {
        using var client = fixture.ManagerClient();
        var draft = Draft();
        var first = await Publish(client, draft);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE procedures ADD CONSTRAINT test_publish_rollback CHECK (title <> 'Reject database write for test')");
        try
        {
            draft.Title = "Reject database write for test";
            using var response = await client.PostAsJsonAsync(Route + "/publish", draft);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(first.Title, (await db.Procedures.AsNoTracking().SingleAsync(x => x.Id == first.Id)).Title);
            Assert.Equal(1, await db.ProcedureVersions.CountAsync(x => x.ProcedureId == first.Id));
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE procedures DROP CONSTRAINT test_publish_rollback"); }
    }

    [Fact]
    public async Task PublicLimitManagerLimitAndPublishAuthorizationAreEnforced()
    {
        using var anonymous = fixture.Factory.CreateClient();
        using var citizen = fixture.ManagerClient("REGISTERED_CITIZEN");
        using var admin = fixture.ManagerClient("IT_ADMIN");
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/v1/procedures?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/v1/procedures?pageSize=50")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Route + "?pageSize=100")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route + "/publish", Draft())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.PostAsJsonAsync(Route + "/publish", Draft())).StatusCode);
        await Publish(admin, Draft());
        var swagger = await admin.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(swagger.GetProperty("paths").TryGetProperty(Route + "/publish", out _));
        Assert.False(swagger.GetProperty("paths").TryGetProperty(Route + "/import-csv", out _));
    }
}
