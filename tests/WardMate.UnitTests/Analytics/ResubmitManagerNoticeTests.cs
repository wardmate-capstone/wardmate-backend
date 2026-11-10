using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.UnitTests.Analytics;

// NOTIFY-RESUBMIT-01: hồ sơ đã gỡ cán bộ được nộp lại → báo thêm Manager của phường.
public sealed class ResubmitManagerNoticeTests
{
    private static readonly Guid Applicant = Guid.NewGuid();
    private static readonly RecipientContact Manager = new(Guid.NewGuid(), "ld@example.test", "W01", false, IsStaffingManager: true);
    private static readonly RecipientContact OtherWardManager = new(Guid.NewGuid(), "ld2@example.test", "W02", false, IsStaffingManager: true);

    private static WorkflowEvent Submitted(Guid? assigned, bool? resubmitted) =>
        new(Guid.NewGuid(), 1, Guid.NewGuid(), Applicant, assigned, "W01", "HS-1", "STATUS", "SUBMITTED", DateTime.UtcNow, Resubmitted: resubmitted);

    [Fact]
    public void Released_resubmission_notifies_ward_manager()
    {
        var e = Submitted(null, true);
        Assert.True(NotificationInboxWorker.ResubmittedToPool(e));
        Assert.True(NotificationInboxWorker.StatusRecipient(e, Manager));
        Assert.False(NotificationInboxWorker.StatusRecipient(e, OtherWardManager));
    }

    [Theory]
    [InlineData(false, null)]   // nộp lần đầu
    [InlineData(true, true)]    // nộp lại nhưng cán bộ cũ vẫn giữ
    public void Other_submissions_do_not_notify_manager(bool assigned, bool? resubmitted)
    {
        var e = Submitted(assigned ? Guid.NewGuid() : null, resubmitted);
        Assert.False(NotificationInboxWorker.StatusRecipient(e, Manager));
    }

    [Fact]
    public void Manager_notice_is_not_ward_scoped_so_delivery_keeps_it()
    {
        var n = NotificationInboxWorker.Create(Submitted(null, true), Manager.UserId, officer: true, manager: true);
        Assert.Null(n.RecipientWardCode);
        Assert.Equal("Hồ sơ nộp lại chưa có cán bộ thụ lý", n.Title);
    }
}
