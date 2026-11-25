using WardMate.Services.DocumentForm.Application.Services;

namespace WardMate.UnitTests.DocumentForm;

// DOC-READ-WARD-01: cán bộ cùng phường được đọc tài liệu hồ sơ đã gửi (chế độ chỉ xem); quyền ghi vẫn chỉ cán bộ thụ lý.
public sealed class ApplicationReadRulesTests
{
    private static readonly Guid Owner = Guid.NewGuid(), Assignee = Guid.NewGuid(), Other = Guid.NewGuid();

    [Fact]
    public void Citizen_reads_only_own_application()
    {
        Assert.True(ApplicationReadRules.Allowed(Owner, null, "SUBMITTED", Owner, officer: false, wardRead: false));
        Assert.False(ApplicationReadRules.Allowed(Owner, null, "SUBMITTED", Other, officer: false, wardRead: true));
    }

    [Fact]
    public void Assigned_officer_keeps_access_without_ward_read()
    {
        Assert.True(ApplicationReadRules.Allowed(Owner, Assignee, "UNDER_REVIEW", Assignee, officer: true, wardRead: false));
        Assert.False(ApplicationReadRules.Allowed(Owner, Assignee, "UNDER_REVIEW", Other, officer: true, wardRead: false));
    }

    [Theory]
    [InlineData("SUBMITTED", true)]
    [InlineData("NEED_REVISION", true)]
    [InlineData("DOCUMENTS_READY", true)]
    [InlineData("DRAFT", false)]
    public void Ward_officer_reads_submitted_applications_but_not_drafts(string status, bool expected)
    {
        Assert.Equal(expected, ApplicationReadRules.Allowed(Owner, null, status, Other, officer: true, wardRead: true));
    }

    [Fact]
    public void Officer_never_reads_own_application_through_officer_path()
    {
        Assert.False(ApplicationReadRules.Allowed(Owner, Owner, "UNDER_REVIEW", Owner, officer: true, wardRead: true));
    }
}
