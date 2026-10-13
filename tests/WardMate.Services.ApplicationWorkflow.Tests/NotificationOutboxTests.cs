using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.EventBus;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class NotificationOutboxTests(WorkflowFixture f) : IClassFixture<WorkflowFixture>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulTransitionsHaveAtomicEventsButRejectedRequestsDoNot(bool approve)
    {
        var owner = Guid.NewGuid(); var officerId = Guid.NewGuid();
        using var citizen = f.Client(owner); using var officer = f.Client(officerId, officer: true);
        var response = await citizen.PostAsJsonAsync("/api/v1/applications", new
        { procedureId = WorkflowFixture.EmptyProcedureId, wardCode = "WARD_A", formData = new { test = "không đưa vào message" } });
        response.EnsureSuccessStatusCode(); var a = (await response.Content.ReadFromJsonAsync<ApplicationDto>())!;
        Assert.Empty(await Events(a.Id));
        (await citizen.PostAsync($"/api/v1/applications/{a.Id}/submit", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await citizen.PostAsync($"/api/v1/applications/{a.Id}/submit", null)).StatusCode);
        (await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/assign", null)).EnsureSuccessStatusCode();
        var final = approve ? await officer.PostAsync($"/api/v1/officer/applications/{a.Id}/approve", null)
            : await officer.PostAsJsonAsync($"/api/v1/officer/applications/{a.Id}/reject", new { reason = "Kiểm thử từ chối" });
        final.EnsureSuccessStatusCode();
        var events = await Events(a.Id);
        Assert.Equal(3, events.Length); Assert.Equal(3, events.Select(x => x.EventId).Distinct().Count());
        Assert.Contains(events, x => x.Status == "SUBMITTED" && x.AssignedOfficerId is null);
        Assert.Contains(events, x => x.Status == "UNDER_REVIEW" && x.AssignedOfficerId == officerId);
        Assert.Contains(events, x => x.Status == (approve ? "APPROVED" : "REJECTED"));
        Assert.All(events, x => { Assert.Equal(owner, x.ApplicantId); Assert.Equal("WARD_A", x.WardCode); Assert.Equal(1, x.Version); });
    }

    [Fact]
    public async Task RollbackRemovesApplicationAndItsOutboxTogether()
    {
        var id = Guid.NewGuid();
        await using (var scope = f.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            db.Applications.Add(new() { Id = id, UserId = Guid.NewGuid(), ProcedureId = WorkflowFixture.EmptyProcedureId,
                ApplicationCode = "ROLLBACK-" + id.ToString("N"), ProcedureTitle = "Test", WardCode = "WARD_A", Status = "SUBMITTED",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                History = [new() { ApplicationId = id, ToStatus = "SUBMITTED", ChangedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }] });
            await db.SaveChangesAsync(); Assert.Single(await Events(db, id));
            await tx.RollbackAsync();
        }
        Assert.Empty(await Events(id));
    }
    private async Task<WorkflowEvent[]> Events(Guid id)
    {
        await using var scope = f.Factory.Services.CreateAsyncScope();
        return await Events(scope.ServiceProvider.GetRequiredService<WorkflowDbContext>(), id);
    }
    private static async Task<WorkflowEvent[]> Events(WorkflowDbContext db, Guid id) =>
        (await db.Outbox.AsNoTracking().Select(x => x.Payload).ToArrayAsync()).Select(x => JsonSerializer.Deserialize<WorkflowEvent>(x)!)
        .Where(x => x.ApplicationId == id).ToArray();
}
