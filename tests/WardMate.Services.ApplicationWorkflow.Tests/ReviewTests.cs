using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class ReviewTests(WorkflowFixture fixture) : IClassFixture<WorkflowFixture>
{
    private static async Task<ApplicationDto> Submit(HttpClient owner, bool checklist = false)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/applications", new
        {
            wardCode = "WARD_A", procedureId = checklist ? WorkflowFixture.ProcedureId : WorkflowFixture.EmptyProcedureId,
            caseCode = checklist ? "A" : null, formData = new { name = "Tên cũ", address = new { city = "Cũ" }, values = new[] { 1, 2 } }
        });
        response.EnsureSuccessStatusCode();
        var a = (await response.Content.ReadFromJsonAsync<ApplicationDto>())!;
        foreach (var c in a.Checklists.Where(x => x.IsRequired))
            (await owner.PatchAsJsonAsync($"/api/v1/applications/{a.Id}/checklists/{c.Id}", new { status = "COMPLETED", fileUrl = "https://example.invalid/old.pdf" })).EnsureSuccessStatusCode();
        var submitted = await owner.PostAsync($"/api/v1/applications/{a.Id}/submit", null);
        submitted.EnsureSuccessStatusCode();
        return (await submitted.Content.ReadFromJsonAsync<ApplicationDto>())!;
    }
    private static async Task Comment(HttpClient officer, Guid id, int version = 1, string type = "FORM_FIELD", string target = "/formData/name") =>
        (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{id}/comments", new
        { versionNumber = version, targetType = type, targetId = target, fieldLabel = "Họ tên", commentText = "Vui lòng sửa theo giấy tờ." })).EnsureSuccessStatusCode();
    private static async Task RequestRevision(HttpClient officer, Guid id) =>
        (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{id}/request-revision", new { reason = "Thông tin chưa đúng." })).EnsureSuccessStatusCode();
    private static object Payload(ApplicationDto a, int version = 1) => new
    {
        expectedVersionNumber = version, formData = new { name = "Tên mới", address = new { city = "Mới" }, values = new[] { 1, 3 } },
        checklists = a.Checklists.Select(x => new { x.Id, status = "COMPLETED", fileUrl = "https://example.invalid/new.pdf", note = (string?)null })
    };

    [Fact]
    public async Task ReviewResubmitSnapshotsCommentsAndDiffAreAtomicAndAccurate()
    {
        using var owner = fixture.Client(Guid.NewGuid()); using var officer = fixture.Client(Guid.NewGuid(), officer: true);
        var a = await Submit(owner, true);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        await Comment(officer, a.Id);
        var checklist = a.Checklists.First(x => x.IsRequired);
        await Comment(officer, a.Id, type: "CHECKLIST_ITEM", target: checklist.Id.ToString());
        await RequestRevision(officer, a.Id);
        var needRevision = await owner.GetFromJsonAsync<ApplicationDto>($"/api/v1/applications/{a.Id}");
        Assert.Equal("NEED_REVISION", needRevision!.Status);
        var response = await owner.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", Payload(a));
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<ReviewResultDto>())!;
        Assert.Equal(1, result.ResubmitCount); Assert.Equal(2, result.CurrentVersionNumber);
        Assert.Equal(a.ApplicationCode, result.Application.ApplicationCode); Assert.Equal("SUBMITTED", result.Application.Status);
        var versions = (await owner.GetFromJsonAsync<VersionDto[]>($"/api/v1/applications/{a.Id}/versions"))!;
        Assert.Equal(2, versions.Length);
        Assert.Equal("Tên cũ", versions[0].SnapshotData.GetProperty("formData").GetProperty("name").GetString());
        Assert.Equal("Tên mới", versions[1].SnapshotData.GetProperty("formData").GetProperty("name").GetString());
        var comments = (await owner.GetFromJsonAsync<CommentDto[]>($"/api/v1/applications/{a.Id}/comments"))!;
        Assert.Equal(2, comments.Length); Assert.All(comments, x => Assert.Equal("RESOLVED", x.Status));
        Assert.Empty((await owner.GetFromJsonAsync<CommentDto[]>($"/api/v1/applications/{a.Id}/comments?status=OPEN"))!);
        var diff = (await officer.GetFromJsonAsync<FieldDiffDto[]>($"/api/v1/applications/{a.Id}/diff?fromVersion=1&toVersion=2"))!;
        Assert.Contains(diff, x => x.FieldKey == "/formData/name" && x.AssociatedComments.Length == 1);
        Assert.Contains(diff, x => x.FieldKey == "/formData/address/city");
        Assert.Contains(diff, x => x.FieldKey == "/formData/values/1");
        Assert.Contains(diff, x => x.FieldKey == $"/checklists/{checklist.Id}/fileUrl" && x.AssociatedComments.Length == 1);
        Assert.All(diff, x => Assert.True(x.HasChanged));
        Assert.Equal(5, result.Application.History.Length);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        await Comment(officer, a.Id, 2); await RequestRevision(officer, a.Id);
        (await owner.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", Payload(a, 2))).EnsureSuccessStatusCode();
        Assert.Equal(3, (await owner.GetFromJsonAsync<VersionDto[]>($"/api/v1/applications/{a.Id}/versions"))!.Length);
    }

    [Fact]
    public async Task RequestRevisionWithoutOpenCommentFailsAndDoesNotChangeHistory()
    {
        using var owner = fixture.Client(Guid.NewGuid()); using var officer = fixture.Client(Guid.NewGuid(), officer: true);
        var a = await Submit(owner);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/request-revision", new { reason = "Sai" })).StatusCode);
        var saved = await owner.GetFromJsonAsync<ApplicationDto>($"/api/v1/applications/{a.Id}");
        Assert.Equal("UNDER_REVIEW", saved!.Status); Assert.Equal(3, saved.History.Length);
    }

    [Fact]
    public async Task RoleOwnerAndAssignedOfficerRestrictionsAreEnforced()
    {
        using var owner = fixture.Client(Guid.NewGuid()); using var officer = fixture.Client(Guid.NewGuid(), officer: true);
        using var other = fixture.Client(Guid.NewGuid(), officer: true); using var citizen = fixture.Client(Guid.NewGuid());
        var a = await Submit(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).StatusCode);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/request-revision", new { reason = "Sai" })).StatusCode);
        foreach (var client in new[] { other, citizen })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/applications/{a.Id}/versions")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/applications/{a.Id}/comments")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/applications/{a.Id}/diff?fromVersion=1&toVersion=2")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", Payload(a))).StatusCode);
        }
    }

    [Fact]
    public async Task ConcurrentAssignmentAndResubmitHaveSingleWinner()
    {
        using var owner = fixture.Client(Guid.NewGuid()); using var officer = fixture.Client(Guid.NewGuid(), officer: true);
        using var other = fixture.Client(Guid.NewGuid(), officer: true);
        var a = await Submit(owner);
        var assigned = await Task.WhenAll(officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null), other.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null));
        Assert.Single(assigned, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(assigned, x => x.StatusCode == HttpStatusCode.Conflict);
        var winner = assigned[0].IsSuccessStatusCode ? officer : other;
        await Comment(winner, a.Id); await RequestRevision(winner, a.Id);
        var responses = await Task.WhenAll(owner.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", Payload(a)), owner.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", Payload(a)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, (await owner.GetFromJsonAsync<VersionDto[]>($"/api/v1/applications/{a.Id}/versions"))!.Length);
    }

    [Fact]
    public async Task InvalidTargetsStaleVersionsAndIncompleteChecklistAreRejected()
    {
        using var owner = fixture.Client(Guid.NewGuid()); using var officer = fixture.Client(Guid.NewGuid(), officer: true);
        var a = await Submit(owner, true);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        foreach (var (version, target, code) in new[] { (1, "/formData/missing", HttpStatusCode.BadRequest), (2, "/formData/name", HttpStatusCode.Conflict) })
            Assert.Equal(code, (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/comments", new
            { versionNumber = version, targetType = "FORM_FIELD", targetId = target, fieldLabel = "Tên", commentText = "Sai" })).StatusCode);
        await Comment(officer, a.Id); await RequestRevision(officer, a.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", Payload(a, 3))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", new
        { expectedVersionNumber = 1, formData = new { name = "Mới" }, checklists = a.Checklists.Select(x => new { x.Id, status = "PENDING" }) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", new
        { expectedVersionNumber = 1, formData = new { }, checklists = Array.Empty<object>() })).StatusCode);
        var saved = await owner.GetFromJsonAsync<ApplicationDto>($"/api/v1/applications/{a.Id}");
        Assert.Equal("NEED_REVISION", saved!.Status); Assert.Equal("Tên cũ", saved.FormData.GetProperty("name").GetString());
        Assert.Single((await owner.GetFromJsonAsync<VersionDto[]>($"/api/v1/applications/{a.Id}/versions"))!);
        Assert.Single((await owner.GetFromJsonAsync<CommentDto[]>($"/api/v1/applications/{a.Id}/comments?status=OPEN"))!);
    }

    [Fact]
    public async Task PendingFiltersPaginationAndFifoWork()
    {
        using var owner = fixture.Client(Guid.NewGuid()); using var officer = fixture.Client(Guid.NewGuid(), officer: true);
        var start = DateTimeOffset.UtcNow;
        var a = await Submit(owner); var b = await Submit(owner);
        var page = (await officer.GetFromJsonAsync<ApplicationPage>($"/api/v1/officer/applications/pending?status=SUBMITTED&procedureId={WorkflowFixture.EmptyProcedureId}&submittedFrom={Uri.EscapeDataString(start.ToString("O"))}&pageSize=1"))!;
        Assert.Equal(2, page.Total); Assert.Equal(a.Id, Assert.Single(page.Items).Id);
        var next = (await officer.GetFromJsonAsync<ApplicationPage>($"/api/v1/officer/applications/pending?submittedFrom={Uri.EscapeDataString(start.ToString("O"))}&pageSize=1&page=2"))!;
        Assert.Equal(b.Id, Assert.Single(next.Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.GetAsync("/api/v1/officer/applications/pending?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.GetAsync("/api/v1/officer/applications/pending?status=DRAFT")).StatusCode);
    }

    [Fact]
    public async Task SchemaUsesJsonbAndUniqueVersions()
    {
        using var owner = fixture.Client(Guid.NewGuid()); var a = await Submit(owner);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        Assert.Equal("jsonb", db.Model.FindEntityType(typeof(Domain.ApplicationVersion))!.FindProperty("SnapshotData")!.GetColumnType());
        db.Versions.Add(new() { ApplicationId = a.Id, VersionNumber = 1, SubmittedBy = a.UserId, SubmittedAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

