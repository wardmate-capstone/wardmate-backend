using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.UnitTests.Analytics;

// ANA-11 (M-10): thống kê thủ tục trả tên kể cả thủ tục đã ẩn, lấy từ bản chụp tên trong hồ sơ do Workflow gửi.
public sealed class ProcedureTitleStatisticsTests
{
    [Fact]
    public void Titles_are_mapped_and_blank_or_empty_ids_are_skipped()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var facts = new DeadlineFacts(1, DateTime.UtcNow, [],
            [new(a, " Đăng ký hộ kinh doanh "), new(b, "  "), new(Guid.Empty, "X"), new(a, "Tên khác")]);
        var titles = AnalyticsService.ProcedureTitles(facts);
        Assert.Equal("Đăng ký hộ kinh doanh", titles[a]);
        Assert.False(titles.ContainsKey(b));
        Assert.Single(titles);
    }

    [Fact]
    public void Old_workflow_without_titles_gives_empty_map()
    {
        Assert.Empty(AnalyticsService.ProcedureTitles(new DeadlineFacts(1, DateTime.UtcNow, [])));
    }
}
