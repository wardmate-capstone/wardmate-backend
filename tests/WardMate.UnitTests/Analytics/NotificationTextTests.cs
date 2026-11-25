using WardMate.EventBus;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.UnitTests.Analytics;

// NOTIFY-TEXT-01: tiêu đề và nội dung thông báo nói rõ việc đã xảy ra thay cho câu chung "có cập nhật".
public sealed class NotificationTextTests
{
    private static readonly Guid Citizen = Guid.NewGuid(), Officer = Guid.NewGuid();

    private static WorkflowEvent Status(string status, Guid? assigned = null, bool? resubmitted = null, string kind = "STATUS") =>
        new(Guid.NewGuid(), 1, Guid.NewGuid(), Citizen, assigned, "W01", "HS-1", kind, status, DateTime.UtcNow, Resubmitted: resubmitted);

    [Fact]
    public void Resubmission_has_its_own_title_and_body_for_officer_and_citizen()
    {
        var e = Status("SUBMITTED", Officer, resubmitted: true);
        var officerNotice = NotificationInboxWorker.Create(e, Officer, officer: true);
        var citizenNotice = NotificationInboxWorker.Create(e, Citizen, officer: false);

        Assert.Equal("Công dân đã gửi lại hồ sơ", officerNotice.Title);
        Assert.Contains("bổ sung và gửi lại hồ sơ HS-1", officerNotice.Content);
        Assert.Equal("Hồ sơ đã được gửi lại", citizenNotice.Title);
        Assert.Contains("gửi lại", citizenNotice.Content);
    }

    [Fact]
    public void First_submission_keeps_existing_titles()
    {
        var e = Status("SUBMITTED");
        Assert.Equal("Có hồ sơ cần tiếp nhận", NotificationInboxWorker.Create(e, Officer, officer: true).Title);
        Assert.Equal("Hồ sơ đã được nộp", NotificationInboxWorker.Create(e, Citizen, officer: false).Title);
        Assert.Contains("đang chờ cán bộ tiếp nhận", NotificationInboxWorker.Create(e, Officer, officer: true).Content);
    }

    [Theory]
    [InlineData("NEED_REVISION", "cần bổ sung")]
    [InlineData("DOCUMENTS_READY", "mã QR")]
    [InlineData("UNDER_REVIEW", "đang kiểm tra")]
    [InlineData("CANCELLED", "đã được hủy")]
    public void Citizen_body_describes_the_status(string status, string expected)
    {
        var n = NotificationInboxWorker.Create(Status(status, Officer), Citizen, officer: false);
        Assert.Contains(expected, n.Content);
        Assert.DoesNotContain("có cập nhật", n.Content);
    }

    [Fact]
    public void Comment_and_assignment_bodies_are_specific()
    {
        Assert.Contains("thêm nhận xét", NotificationInboxWorker.Create(Status("UNDER_REVIEW", Officer, kind: "COMMENT"), Citizen, officer: false).Content);
        var release = new WorkflowEvent(Guid.NewGuid(), 2, Guid.NewGuid(), Citizen, null, "W01", "HS-1", "APPLICATION_ASSIGNED", "SUBMITTED",
            DateTime.UtcNow, Officer, "RELEASE");
        Assert.Contains("trả về hàng đợi", NotificationInboxWorker.Create(release, Guid.NewGuid(), officer: true).Content);
    }
}
