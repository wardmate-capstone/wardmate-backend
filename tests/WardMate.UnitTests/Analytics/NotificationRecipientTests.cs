using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.UnitTests.Analytics;

// NOTIFY-COMMENT-01 (F1): người dân không nhận thông báo cho từng nhận xét khi cán bộ còn đang kiểm tra.
public sealed class NotificationRecipientTests
{
    private static readonly Guid Applicant = Guid.NewGuid(), Officer = Guid.NewGuid(), Other = Guid.NewGuid();
    private static readonly RecipientContact Citizen = new(Applicant, "dan@example.test", null, false);
    private static readonly RecipientContact AssignedOfficer = new(Officer, "cb@example.test", "W01", true);
    private static readonly RecipientContact WardOfficer = new(Other, "cb2@example.test", "W01", true);

    private static WorkflowEvent Event(string kind, string status, Guid? assigned) =>
        new(Guid.NewGuid(), 1, Guid.NewGuid(), Applicant, assigned, "W01", "HS-1", kind, status, DateTime.UtcNow);

    [Fact]
    public void Comment_event_notifies_nobody()
    {
        var e = Event("COMMENT", "UNDER_REVIEW", Officer);
        Assert.False(NotificationInboxWorker.StatusRecipient(e, Citizen));
        Assert.False(NotificationInboxWorker.StatusRecipient(e, AssignedOfficer));
    }

    [Fact]
    public void Need_revision_still_notifies_applicant_only()
    {
        var e = Event("STATUS", "NEED_REVISION", Officer);
        Assert.True(NotificationInboxWorker.StatusRecipient(e, Citizen));
        Assert.False(NotificationInboxWorker.StatusRecipient(e, AssignedOfficer));
    }

    [Fact]
    public void Unassigned_submission_notifies_ward_pool()
    {
        var e = Event("STATUS", "SUBMITTED", null);
        Assert.True(NotificationInboxWorker.StatusRecipient(e, Citizen));
        Assert.True(NotificationInboxWorker.StatusRecipient(e, WardOfficer));
    }

    [Fact]
    public void Resubmission_notifies_assigned_officer_not_pool()
    {
        var e = Event("STATUS", "SUBMITTED", Officer);
        Assert.True(NotificationInboxWorker.StatusRecipient(e, AssignedOfficer));
        Assert.False(NotificationInboxWorker.StatusRecipient(e, WardOfficer));
    }
}
