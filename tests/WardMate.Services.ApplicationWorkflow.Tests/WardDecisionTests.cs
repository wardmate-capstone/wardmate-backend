using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class WardDecisionTests(WorkflowFixture f) : IClassFixture<WorkflowFixture>
{
    private static async Task<ApplicationDto> Submit(HttpClient citizen, string ward = "WARD_A")
    {
        var create = await citizen.PostAsJsonAsync("/api/v1/applications", new
        { procedureId = WorkflowFixture.EmptyProcedureId, wardCode = ward, formData = new { fullName = "Kiểm thử" } });
        create.EnsureSuccessStatusCode(); var a = (await create.Content.ReadFromJsonAsync<ApplicationDto>())!;
        var response = await citizen.PostAsync($"/api/v1/applications/{a.Id}/submit", null); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDto>())!;
    }
    [Fact]
    public async Task QueueAndEveryReviewActionAreWardScopedEvenAfterTransfer()
    {
        using var citizen = f.Client(Guid.NewGuid()); var officerId = Guid.NewGuid();
        using var officer = f.Client(officerId, officer: true); using var other = f.Client(Guid.NewGuid(), officer: true, wardCode: "WARD_B");
        var a = await Submit(citizen); var b = await Submit(citizen, "WARD_B");
        var queue = (await officer.GetFromJsonAsync<ApplicationPage>("/api/v1/officer/applications/pending"))!;
        Assert.Contains(queue.Items, x => x.Id == a.Id); Assert.DoesNotContain(queue.Items, x => x.Id == b.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).StatusCode);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        f.Directory.Users[officerId] = f.Directory.Users[officerId] with { WardCode = "WARD_B" };
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/reject", new { reason = "Sai" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/comments", new
        { versionNumber = 1, targetType = "FORM_FIELD", targetId = "/formData/fullName", fieldLabel = "Tên", commentText = "Sai" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await officer.GetAsync($"/api/v1/applications/{a.Id}/versions")).StatusCode);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FinalDecisionWritesHistoryAndFreezesEveryMutation(bool approve)
    {
        using var citizen = f.Client(Guid.NewGuid()); using var officer = f.Client(Guid.NewGuid(), officer: true);
        var a = await Submit(citizen);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        var response = approve ? await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/approve", null)
            : await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/reject", new { reason = "Không đáp ứng điều kiện." });
        response.EnsureSuccessStatusCode(); var result = (await response.Content.ReadFromJsonAsync<ReviewResultDto>())!;
        Assert.Equal(approve ? "APPROVED" : "REJECTED", result.Application.Status);
        Assert.Equal(approve, result.Application.ApprovedAt.HasValue);
        Assert.Equal(4, result.Application.History.Length);
        if (!approve) Assert.Equal("Không đáp ứng điều kiện.", result.Application.History.Last().Reason);
        Assert.Equal(HttpStatusCode.Conflict, (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/comments", new
        { versionNumber = 1, targetType = "FORM_FIELD", targetId = "/formData/fullName", fieldLabel = "Tên", commentText = "Sai" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await citizen.PostAsJsonAsync($"/api/v1/applications/{a.Id}/resubmit", new
        { expectedVersionNumber = 1, formData = new { fullName = "Sửa sau quyết định" }, checklists = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await citizen.PostAsync($"/api/v1/applications/{a.Id}/submit", null)).StatusCode);
        Assert.Single((await citizen.GetFromJsonAsync<VersionDto[]>($"/api/v1/applications/{a.Id}/versions"))!);
    }
    [Fact]
    public async Task RequiredWardAndRejectReasonAreValidatedAndCommentsBlockApproval()
    {
        using var citizen = f.Client(Guid.NewGuid()); using var officer = f.Client(Guid.NewGuid(), officer: true);
        foreach (var ward in new string?[] { null, "UNKNOWN" })
            Assert.Equal(HttpStatusCode.BadRequest, (await citizen.PostAsJsonAsync("/api/v1/applications", new
            { procedureId = WorkflowFixture.EmptyProcedureId, wardCode = ward, formData = new { } })).StatusCode);
        var a = await Submit(citizen);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/approve", null)).StatusCode);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/reject", new { reason = " " })).StatusCode);
        (await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/comments", new
        { versionNumber = 1, targetType = "FORM_FIELD", targetId = "/formData/fullName", fieldLabel = "Tên", commentText = "Sai" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/approve", null)).StatusCode);
    }
    [Fact]
    public async Task ConcurrentApproveAndRejectHaveOneWinner()
    {
        using var citizen = f.Client(Guid.NewGuid()); using var officer = f.Client(Guid.NewGuid(), officer: true);
        var a = await Submit(citizen); (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        var results = await Task.WhenAll(officer.PostAsync($"/api/v1/officer/applications/{a.Id}/approve", null),
            officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/reject", new { reason = "Không đủ điều kiện." }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        Assert.Equal(1, await db.StatusHistory.CountAsync(x => x.ApplicationId == a.Id && (x.ToStatus == "APPROVED" || x.ToStatus == "REJECTED")));
    }
    [Fact]
    public async Task PermissionRemovalAndMissingWardTakeEffectWithSameToken()
    {
        var id = Guid.NewGuid(); using var officer = f.Client(id, officer: true);
        f.Directory.Users[id] = f.Directory.Users[id] with { Permissions = [] };
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/v1/officer/applications/pending")).StatusCode);
        f.Directory.Users[id] = f.Directory.Users[id] with { Permissions = ["workflow.queue.read"], WardCode = null };
        Assert.Empty((await officer.GetFromJsonAsync<ApplicationPage>("/api/v1/officer/applications/pending"))!.Items);
        f.Directory.Users.TryRemove(id, out _);
        Assert.Equal(HttpStatusCode.Unauthorized, (await officer.GetAsync("/api/v1/officer/applications/pending")).StatusCode);
    }
    [Fact]
    public async Task IamUnavailableFailsClosed()
    {
        using var officer = f.Client(Guid.NewGuid(), officer: true);
        f.Directory.Unavailable = true;
        try { Assert.Equal(HttpStatusCode.ServiceUnavailable, (await officer.GetAsync("/api/v1/officer/applications/pending")).StatusCode); }
        finally { f.Directory.Unavailable = false; }
    }
}
