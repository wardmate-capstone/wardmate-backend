using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Infrastructure;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.UnitTests.Workflow;

// WF-ASSIGN-REL-01 (F2): gỡ cán bộ khỏi hồ sơ chờ bổ sung; chuyển hồ sơ đã nộp lại qua API phân công cũ.
public sealed class ReleaseAssignmentTests
{
    private static readonly Guid Officer = Guid.NewGuid(), Manager = Guid.NewGuid(), NewOfficer = Guid.NewGuid(), Citizen = Guid.NewGuid();
    private static readonly ReviewActor OfficerActor = new(Officer, true, "W01", FullName: "Cán bộ A");
    private static readonly ReviewActor ManagerActor = new(Manager, false, "W01", IsManager: true, FullName: "Lãnh đạo");
    private static readonly ReleaseApplicationInput Reason = new("Cán bộ nghỉ phép dài ngày");

    private static ApplicationRecord Application(string status) => new()
    {
        UserId = Citizen, WardCode = "W01", Status = status, AssignedOfficerId = Officer, AssignedOfficerName = "Cán bộ A",
        ProcedureTitle = "Thủ tục", FormData = "{}", Versions = { new ApplicationVersion { VersionNumber = 1, SnapshotData = "{}" } }
    };

    private static async Task<WorkflowResult<ReviewResultDto>> Release(ApplicationRecord a, ReviewActor actor, bool asManager) =>
        await new AssignmentHandlers(new Store(a), TimeProvider.System).Handle(new ReleaseApplicationCommand(actor, a.Id, Reason, asManager), default);

    [Fact]
    public async Task Officer_can_release_need_revision_and_status_stays()
    {
        var a = Application(ApplicationStates.NeedRevision);
        var result = await Release(a, OfficerActor, asManager: false);
        Assert.Null(result.Error);
        Assert.Null(a.AssignedOfficerId);
        Assert.Equal(ApplicationStates.NeedRevision, a.Status);
        Assert.Equal("RELEASE", Assert.Single(a.AssignmentHistory).Action);
        Assert.Empty(a.History);
    }

    [Fact]
    public async Task Manager_can_release_need_revision_for_absent_officer()
    {
        var a = Application(ApplicationStates.NeedRevision);
        Assert.Null((await Release(a, ManagerActor, asManager: true)).Error);
        Assert.Null(a.AssignedOfficerId);
    }

    [Fact]
    public async Task Manager_cannot_release_case_under_review()
    {
        var a = Application(ApplicationStates.UnderReview);
        Assert.Equal("workflow.invalid_status", (await Release(a, ManagerActor, asManager: true)).Error!.Code);
        Assert.Equal(Officer, a.AssignedOfficerId);
    }

    [Fact]
    public async Task Officer_release_under_review_still_returns_to_submitted()
    {
        var a = Application(ApplicationStates.UnderReview);
        Assert.Null((await Release(a, OfficerActor, asManager: false)).Error);
        Assert.Equal(ApplicationStates.Submitted, a.Status);
    }

    [Fact]
    public async Task Other_officer_cannot_release()
    {
        var a = Application(ApplicationStates.NeedRevision);
        var other = new ReviewActor(Guid.NewGuid(), true, "W01");
        Assert.Equal("workflow.not_assignee", (await Release(a, other, asManager: false)).Error!.Code);
    }

    [Fact]
    public async Task Resubmitted_case_can_be_transferred_by_manager()
    {
        var a = Application(ApplicationStates.Submitted);
        var command = new ManageAssigneeCommand(ManagerActor, a.Id, new ManageAssigneeInput(NewOfficer, "Cán bộ cũ vắng mặt", 0),
            new OfficerTarget(NewOfficer, "Cán bộ B", "W01"));
        var result = await new AssignmentHandlers(new Store(a), TimeProvider.System).Handle(command, default);
        Assert.Null(result.Error);
        Assert.Equal(NewOfficer, a.AssignedOfficerId);
        Assert.Equal(ApplicationStates.Submitted, a.Status);
        Assert.Equal("TRANSFER", Assert.Single(a.AssignmentHistory).Action);
    }

    [Fact]
    public void Release_while_need_revision_does_not_notify_ward_pool()
    {
        var e = new WorkflowEvent(Guid.NewGuid(), 2, Guid.NewGuid(), Citizen, null, "W01", "HS-1", "APPLICATION_ASSIGNED",
            ApplicationStates.NeedRevision, DateTime.UtcNow, Officer, "RELEASE");
        Assert.False(NotificationInboxWorker.AssignmentRecipient(e, new RecipientContact(NewOfficer, "b@example.test", "W01", true)));
        var underReview = e with { Status = ApplicationStates.Submitted };
        Assert.True(NotificationInboxWorker.AssignmentRecipient(underReview, new RecipientContact(NewOfficer, "b@example.test", "W01", true)));
    }

    private sealed class Store(ApplicationRecord application) : IApplicationReviewStore
    {
        public Task<WorkflowResult<ReviewResultDto>> Mutate(Guid id, Func<ApplicationRecord, WorkflowResult<ReviewResultDto>> action, CancellationToken ct) =>
            Task.FromResult(action(application));
        public Task<ApplicationRecord?> Read(ReviewActor actor, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationPage> ListAll(ReviewActor actor, OfficerListInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationPage> Pending(ReviewActor actor, OfficerPendingInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<AssignmentSummaryDto> AssignmentSummary(ReviewActor actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<OfficerActivityPage> Activity(ReviewActor actor, Guid officerId, OfficerActivityInput input, CancellationToken ct) => throw new NotSupportedException();
    }
}
