using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class WorkflowTests(WorkflowFixture fixture) : IClassFixture<WorkflowFixture>
{
    private static async Task<ApplicationDto> Create(HttpClient client, Guid? procedureId = null, string? caseCode = "A")
    {
        var response = await client.PostAsJsonAsync("/api/v1/applications", new
        { procedureId = procedureId ?? WorkflowFixture.ProcedureId, caseCode, formData = new { fullName = "Dữ liệu giả", nested = new { values = new[] { 1, 2 } } } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApplicationDto>())!;
    }
    private static async Task Complete(HttpClient client, ApplicationDto application)
    {
        foreach (var item in application.Checklists.Where(x => x.IsRequired))
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/applications/{application.Id}/checklists/{item.Id}",
                new { status = "COMPLETED", fileUrl = "https://example.invalid/proof.pdf", note = "Kiểm thử" })).StatusCode);
    }

    [Fact]
    public async Task CreateSnapshotsOnlyCommonAndSelectedCaseWithHistoryAndJsonb()
    {
        var user = Guid.NewGuid(); using var client = fixture.Client(user);
        var application = await Create(client);
        Assert.Equal("DRAFT", application.Status);
        Assert.Matches("^HS-[0-9]{8}-[0-9]{8,}$", application.ApplicationCode!);
        Assert.Equal(user, application.UserId);
        Assert.Equal(3, application.Checklists.Length);
        Assert.DoesNotContain(application.Checklists, x => x.Code == "CASE_B");
        Assert.All(application.Checklists, x => Assert.Equal("PENDING", x.Status));
        Assert.Equal(2, application.Checklists.Count(x => x.IsRequired));
        Assert.Null(Assert.Single(application.History).FromStatus);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var stored = await db.Applications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(2, JsonSerializer.Deserialize<JsonElement>(stored.FormData).GetProperty("nested").GetProperty("values").GetArrayLength());
        Assert.Equal(3, await db.Checklists.CountAsync(x => x.ApplicationId == application.Id));
        Assert.Equal("jsonb", db.Model.FindEntityType(typeof(Domain.ApplicationRecord))!.FindProperty("FormData")!.GetColumnType());
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("REJECTED")]
    public async Task RequiredIncompleteChecklistBlocksSubmitAndRollsBack(string state)
    {
        using var client = fixture.Client(Guid.NewGuid()); var a = await Create(client);
        var item = a.Checklists.First(x => x.IsRequired);
        await client.PatchAsJsonAsync($"/api/v1/applications/{a.Id}/checklists/{item.Id}", new { status = state });
        var response = await client.PostAsync($"/api/v1/applications/{a.Id}/submit", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("application.checklist_incomplete", error.GetProperty("code").GetString());
        Assert.Equal(2, error.GetProperty("missingItems").GetArrayLength());
        var unchanged = await client.GetFromJsonAsync<ApplicationDto>($"/api/v1/applications/{a.Id}");
        Assert.Equal("DRAFT", unchanged!.Status); Assert.Null(unchanged.SubmittedAt); Assert.Equal(a.ApplicationCode, unchanged.ApplicationCode);
        Assert.Single(unchanged.History);
    }

    [Fact]
    public async Task SubmitOnceWritesCodeAndHistoryAndFreezesChecklist()
    {
        using var client = fixture.Client(Guid.NewGuid()); var a = await Create(client); await Complete(client, a);
        var response = await client.PostAsync($"/api/v1/applications/{a.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var submitted = (await response.Content.ReadFromJsonAsync<ApplicationDto>())!;
        Assert.Equal("SUBMITTED", submitted.Status); Assert.NotNull(submitted.SubmittedAt);
        Assert.Matches("^HS-[0-9]{8}-[0-9]{8,}$", submitted.ApplicationCode!);
        Assert.Equal(2, submitted.History.Length);
        Assert.Single(submitted.History, x => x.FromStatus == "DRAFT" && x.ToStatus == "SUBMITTED");
        Assert.Contains(submitted.Checklists, x => !x.IsRequired && x.Status == "PENDING");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/v1/applications/{a.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/v1/applications/{a.Id}/checklists/{a.Checklists[0].Id}", new { status = "PENDING" })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentSubmitHasSingleWinnerAndHistory()
    {
        using var client = fixture.Client(Guid.NewGuid()); var a = await Create(client); await Complete(client, a);
        var responses = await Task.WhenAll(client.PostAsync($"/api/v1/applications/{a.Id}/submit", null), client.PostAsync($"/api/v1/applications/{a.Id}/submit", null));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var saved = await client.GetFromJsonAsync<ApplicationDto>($"/api/v1/applications/{a.Id}"); Assert.Equal(2, saved!.History.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherOwnerIncludingAdminCannotReadEditOrSubmit(bool admin)
    {
        using var owner = fixture.Client(Guid.NewGuid()); var a = await Create(owner);
        using var stranger = fixture.Client(Guid.NewGuid(), admin);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/applications/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/v1/applications/{a.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PatchAsJsonAsync($"/api/v1/applications/{a.Id}/checklists/{a.Checklists[0].Id}", new { status = "COMPLETED" })).StatusCode);
        var page = await stranger.GetFromJsonAsync<ApplicationPage>("/api/v1/applications"); Assert.Equal(0, page!.Total);
    }

    [Fact]
    public async Task ChecklistIdFromAnotherApplicationCannotBeUpdated()
    {
        using var client = fixture.Client(Guid.NewGuid()); var a = await Create(client); var b = await Create(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/v1/applications/{a.Id}/checklists/{b.Checklists[0].Id}", new { status = "COMPLETED" })).StatusCode);
    }

    [Fact]
    public async Task ChecklistPatchPreservesAttachmentWhenOmittedAndSerializesWithSubmit()
    {
        using var client = fixture.Client(Guid.NewGuid()); var a = await Create(client); await Complete(client, a);
        var item = a.Checklists.First(x => x.IsRequired);
        var url = $"/api/v1/applications/{a.Id}/checklists/{item.Id}";
        var unchanged = await (await client.PatchAsJsonAsync(url, new { status = "COMPLETED" })).Content.ReadFromJsonAsync<ApplicationDto>();
        Assert.Equal("https://example.invalid/proof.pdf", unchanged!.Checklists.Single(x => x.Id == item.Id).FileUrl);
        await Task.WhenAll(client.PostAsync($"/api/v1/applications/{a.Id}/submit", null), client.PatchAsJsonAsync(url, new { status = "PENDING" }));
        var saved = await client.GetFromJsonAsync<ApplicationDto>($"/api/v1/applications/{a.Id}");
        if (saved!.Status == "SUBMITTED") Assert.All(saved.Checklists.Where(x => x.IsRequired), x => Assert.Equal("COMPLETED", x.Status));
        else { Assert.Equal("DRAFT", saved.Status); Assert.Single(saved.History); }
    }

    [Fact]
    public async Task EmptyChecklistAllowsSubmitWithDistinctCodesAndUnknownProcedureFails()
    {
        using var client = fixture.Client(Guid.NewGuid());
        var a = await Create(client, WorkflowFixture.EmptyProcedureId, null); var b = await Create(client, WorkflowFixture.EmptyProcedureId, null);
        var first = await (await client.PostAsync($"/api/v1/applications/{a.Id}/submit", null)).Content.ReadFromJsonAsync<ApplicationDto>();
        var second = await (await client.PostAsync($"/api/v1/applications/{b.Id}/submit", null)).Content.ReadFromJsonAsync<ApplicationDto>();
        Assert.NotNull(first!.ApplicationCode); Assert.NotEqual(first.ApplicationCode, second!.ApplicationCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/applications", new { procedureId = Guid.NewGuid(), formData = new { } })).StatusCode);
    }

    [Fact]
    public async Task ValidationAndAuthenticationRejectInvalidRequests()
    {
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/applications")).StatusCode);
        using var client = fixture.Client(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/applications", new { procedureId = WorkflowFixture.ProcedureId, formData = new { } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/applications", new { procedureId = WorkflowFixture.ProcedureId, caseCode = "A", formData = "not an object" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/applications?pageSize=101")).StatusCode);
        var a = await Create(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/v1/applications/{a.Id}/checklists/{a.Checklists[0].Id}", new { status = "APPROVED" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/v1/applications/{a.Id}/checklists/{a.Checklists[0].Id}", new { status = "COMPLETED", fileUrl = "http://example.invalid/a" })).StatusCode);
    }
}


