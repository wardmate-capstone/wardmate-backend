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

public sealed class ProcedureManagerTests(ProcedureFixture fixture) : IClassFixture<ProcedureFixture>
{
    private const string Route = "/api/v1/procedure-manager/procedures";

    private static UpdateProcedureInput Input()
    {
        var sample = ProcedureSeed.CreateSample();
        return new()
        {
            CategoryId = 1, ProcedureCode = Guid.NewGuid().ToString("N"), Title = "Thủ tục kiểm thử quản lý",
            IssuingAuthority = "Cơ quan thử nghiệm", ExecutingAgency = "Đơn vị thử nghiệm",
            ContentPayload = sample.ContentPayload, ChecklistSchema = sample.ChecklistSchema, FormDefinitions = sample.FormDefinitions,
            DecisionNumber = "QD-TEST-02", EffectiveDate = new DateOnly(2026, 10, 1)
        };
    }

    private static async Task<ProcedureDetailDto> Create(HttpClient client, ProcedureInput input)
    {
        using var response = await client.PostAsJsonAsync(Route, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        Assert.EndsWith(created.Id.ToString(), response.Headers.Location!.ToString());
        return created;
    }

    [Fact]
    public async Task CreatePersistsComplexJsonAndInitialSnapshotAtomically()
    {
        using var client = fixture.ManagerClient();
        var input = Input();
        input.ContentPayload.Cases.Add(new()
        {
            CaseCode = "BO_SUNG", CaseName = "Bổ sung hồ sơ", Steps = [new()
            {
                StepOrder = 1, StepName = "Tiếp nhận bổ sung", Executor = "Cán bộ",
                ActionDetails = "Dữ liệu \"tiếng Việt\"\nvà dòng mới"
            }]
        });
        input.ChecklistSchema!.Add(new() { ChecklistId = "EXTRA", CaseCode = "BO_SUNG", ItemName = "Giấy tờ bổ sung", Quantity = 3 });
        input.FormDefinitions![0].FormTemplateId = Guid.NewGuid();
        var created = await Create(client, input);
        Assert.True(created.IsActive);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var saved = await db.Procedures.SingleAsync(x => x.Id == created.Id);
        Assert.Equal(JsonSerializer.Serialize(input.ContentPayload), JsonSerializer.Serialize(saved.ContentPayload));
        Assert.Equal(JsonSerializer.Serialize(input.ChecklistSchema), JsonSerializer.Serialize(saved.ChecklistSchema));
        Assert.Equal(JsonSerializer.Serialize(input.FormDefinitions), JsonSerializer.Serialize(saved.FormDefinitions));
        var version = await db.ProcedureVersions.SingleAsync(x => x.ProcedureId == created.Id);
        Assert.Equal(1, version.VersionNumber);
        var snapshot = JsonSerializer.Deserialize<JsonElement>(version.SnapshotData);
        Assert.Equal(saved.Id, snapshot.GetProperty("id").GetGuid());
        Assert.Equal("Bổ sung hồ sơ", snapshot.GetProperty("contentPayload").GetProperty("cases")[1].GetProperty("caseName").GetString());
        Assert.Equal(3, snapshot.GetProperty("checklistSchema")[2].GetProperty("quantity").GetInt32());
        Assert.Equal(input.FormDefinitions[0].FormTemplateId, snapshot.GetProperty("formDefinitions")[0].GetProperty("formTemplateId").GetGuid());
    }

    [Fact]
    public async Task UpdatesFreezePreviousStateAndReturnOrderedVersionsAsJsonObjects()
    {
        using var client = fixture.ManagerClient();
        var input = Input();
        var created = await Create(client, input);
        var oldAction = input.ContentPayload.Cases[0].Steps[0].ActionDetails;
        input.Title = "Tên thủ tục lần cập nhật thứ nhất";
        input.CategoryId = 2;
        input.ContentPayload.Cases[0].Steps[0].ActionDetails = "Nội dung lần một";
        input.ChecklistSchema![0].Quantity = 4;
        using var first = await client.PutAsJsonAsync($"{Route}/{created.Id}", input);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        input.Title = "Tên thủ tục lần cập nhật thứ hai";
        input.ContentPayload.Cases[0].Steps[0].ActionDetails = "Nội dung lần hai";
        input.DecisionNumber = "QD-TEST-03";
        using var second = await client.PutAsJsonAsync($"{Route}/{created.Id}", input);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var live = (await second.Content.ReadFromJsonAsync<ProcedureDetailDto>())!;
        Assert.Equal("Đất đai", live.CategoryName);
        Assert.Equal(input.DecisionNumber, live.ContentPayload.DecisionNumber);
        Assert.True(live.UpdatedAt >= created.UpdatedAt);
        var versions = (await client.GetFromJsonAsync<List<ProcedureVersionDto>>($"{Route}/{created.Id}/versions"))!;
        Assert.Equal(new[] { 3, 2, 1 }, versions.Select(x => x.VersionNumber));
        Assert.Equal("QD-TEST-03", versions[0].DecisionNumber);
        Assert.Equal(input.EffectiveDate, versions[0].EffectiveDate);
        Assert.Equal("Tên thủ tục lần cập nhật thứ nhất", versions[0].SnapshotData.GetProperty("title").GetString());
        Assert.Equal("Nội dung lần một", versions[0].SnapshotData.GetProperty("contentPayload").GetProperty("cases")[0].GetProperty("steps")[0].GetProperty("actionDetails").GetString());
        Assert.Equal(created.Title, versions[1].SnapshotData.GetProperty("title").GetString());
        Assert.Equal(oldAction, versions[1].SnapshotData.GetProperty("contentPayload").GetProperty("cases")[0].GetProperty("steps")[0].GetProperty("actionDetails").GetString());
        Assert.Equal(created.Title, versions[2].SnapshotData.GetProperty("title").GetString());
    }

    [Fact]
    public async Task TogglePersistsReasonHidesPublicReadAndCanReopenWithoutExtraVersions()
    {
        using var client = fixture.ManagerClient();
        var input = Input();
        var created = await Create(client, input);
        using var close = await client.PatchAsJsonAsync($"{Route}/{created.Id}/status", new { isActive = false, reason = " Tạm ngưng tiếp nhận " });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
            var saved = await db.Procedures.SingleAsync(x => x.Id == created.Id);
            Assert.False(saved.IsActive);
            Assert.Equal("Tạm ngưng tiếp nhận", saved.StatusChangeReason);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/procedures/{created.Id}")).StatusCode);
        input.Title = "Cập nhật khi thủ tục đang tạm đóng";
        using var update = await client.PutAsJsonAsync($"{Route}/{created.Id}", input);
        Assert.False((await update.Content.ReadFromJsonAsync<ProcedureDetailDto>())!.IsActive);
        var versions = (await client.GetFromJsonAsync<List<ProcedureVersionDto>>($"{Route}/{created.Id}/versions"))!;
        Assert.Equal("Tạm ngưng tiếp nhận", versions[0].SnapshotData.GetProperty("statusChangeReason").GetString());
        using var open = await client.PatchAsJsonAsync($"{Route}/{created.Id}/status", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        var status = (await open.Content.ReadFromJsonAsync<ProcedureStatusDto>())!;
        Assert.True(status.IsActive);
        Assert.Null(status.Reason);
        Assert.DoesNotContain("\"reason\"", await open.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/procedures/{created.Id}")).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<List<ProcedureVersionDto>>($"{Route}/{created.Id}/versions"))!.Count);
    }

    [Fact]
    public async Task ConcurrentUpdatesProduceSequentialVersionsAndConsistentSnapshotChain()
    {
        using var client = fixture.ManagerClient();
        var original = Input();
        var created = await Create(client, original);
        var a = Input(); a.ProcedureCode = original.ProcedureCode; a.Title = "Cập nhật đồng thời nhánh A";
        var b = Input(); b.ProcedureCode = original.ProcedureCode; b.Title = "Cập nhật đồng thời nhánh B";
        var responses = await Task.WhenAll(client.PutAsJsonAsync($"{Route}/{created.Id}", a), client.PutAsJsonAsync($"{Route}/{created.Id}", b));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); response.Dispose(); }
        var versions = (await client.GetFromJsonAsync<List<ProcedureVersionDto>>($"{Route}/{created.Id}/versions"))!;
        Assert.Equal(new[] { 3, 2, 1 }, versions.Select(x => x.VersionNumber));
        Assert.Equal(created.Title, versions[1].SnapshotData.GetProperty("title").GetString());
        var intermediate = versions[0].SnapshotData.GetProperty("title").GetString();
        Assert.Contains(intermediate, new[] { a.Title, b.Title });
        var live = await client.GetFromJsonAsync<ProcedureDetailDto>($"/api/v1/procedures/{created.Id}");
        Assert.NotEqual(intermediate, live!.Title);
    }

    [Theory]
    [InlineData("empty-code")]
    [InlineData("short-title")]
    [InlineData("missing-category")]
    [InlineData("null-content")]
    [InlineData("invalid-checklist")]
    [InlineData("invalid-form")]
    [InlineData("null-case")]
    [InlineData("long-decision")]
    public async Task InvalidCreateReturnsVietnameseProblemAndDoesNotWrite(string scenario)
    {
        using var client = fixture.ManagerClient();
        var input = Input();
        switch (scenario)
        {
            case "empty-code": input.ProcedureCode = " "; break;
            case "short-title": input.Title = "Ngắn"; break;
            case "missing-category": input.CategoryId = int.MaxValue; break;
            case "null-content": input.ContentPayload = null!; break;
            case "invalid-checklist": input.ChecklistSchema![0].Quantity = 0; break;
            case "invalid-form": input.FormDefinitions![0].FormType = "WRONG"; break;
            case "null-case": input.ContentPayload.Cases.Add(null!); break;
            case "long-decision": input.ContentPayload.DecisionNumber = new string('a', 101); break;
        }
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var before = await db.Procedures.CountAsync();
        var versionsBefore = await db.ProcedureVersions.CountAsync();
        using var response = await client.PostAsJsonAsync(Route, input);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(problem.GetProperty("code").GetString(), new[] { "validation.failed", "procedure.category_not_found" });
        Assert.Equal(before, await db.Procedures.CountAsync());
        Assert.Equal(versionsBefore, await db.ProcedureVersions.CountAsync());
    }

    [Fact]
    public async Task DuplicateCodesConflictAndFailedUpdateDoesNotAddSnapshot()
    {
        using var client = fixture.ManagerClient();
        var input = Input();
        var first = await Create(client, input);
        using var duplicate = await client.PostAsJsonAsync(Route, input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var other = await Create(client, Input());
        using var collision = await client.PutAsJsonAsync($"{Route}/{other.Id}", input);
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<ProcedureVersionDto>>($"{Route}/{other.Id}/versions"))!);
        input.DecisionNumber = "";
        input.EffectiveDate = default;
        using var invalid = await client.PutAsJsonAsync($"{Route}/{first.Id}", input);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<ProcedureVersionDto>>($"{Route}/{first.Id}/versions"))!);
    }

    [Fact]
    public async Task ConcurrentCreateWithSameCodeHasOneSuccessAndOneConflict()
    {
        using var client = fixture.ManagerClient();
        var input = Input();
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Route, input), client.PostAsJsonAsync(Route, input));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcedureDbContext>();
        var procedure = await db.Procedures.SingleAsync(x => x.ProcedureCode == input.ProcedureCode);
        Assert.Single(await db.ProcedureVersions.Where(x => x.ProcedureId == procedure.Id).ToListAsync());
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("REGISTERED_CITIZEN", 403)]
    [InlineData("MANAGER", 403)]
    [InlineData("expired", 401)]
    [InlineData("invalid-signature", 401)]
    public async Task EveryManagerEndpointRequiresValidTokenAndRole(string kind, int status)
    {
        using var client = kind == "anonymous" ? fixture.Factory.CreateClient() : fixture.ManagerClient(
            kind is "expired" or "invalid-signature" ? "PROCEDURE_MANAGER" : kind,
            expired: kind == "expired", invalidSignature: kind == "invalid-signature");
        var id = Guid.NewGuid();
        var responses = new[]
        {
            await client.PostAsJsonAsync(Route, Input()), await client.PutAsJsonAsync($"{Route}/{id}", Input()),
            await client.PatchAsJsonAsync($"{Route}/{id}/status", new { isActive = true }), await client.GetAsync($"{Route}/{id}/versions")
        };
        foreach (var response in responses)
        {
            Assert.Equal(status, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            response.Dispose();
        }
    }

    [Fact]
    public async Task AdminCanManageAndMissingProceduresReturn404()
    {
        using var client = fixture.ManagerClient("IT_ADMIN");
        await Create(client, Input());
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Route}/{id}", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"{Route}/{id}/status", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{id}/versions")).StatusCode);
    }

    [Fact]
    public async Task MissingStatusCannotSilentlyDisableProcedureAndSwaggerDeclaresBearer()
    {
        using var client = fixture.ManagerClient();
        var created = await Create(client, Input());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"{Route}/{created.Id}/status", new { reason = "Thiếu trạng thái" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/procedures/{created.Id}")).StatusCode);
        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.Equal("bearer", swagger.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(swagger.GetProperty("paths").GetProperty(Route).GetProperty("post").TryGetProperty("security", out _));
    }
}
