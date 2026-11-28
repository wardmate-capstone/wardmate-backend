using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.UnitTests.Analytics;

// HEALTH-02 (M-15): dịch vụ đang khởi động (minReplicas 0) được đo lại một lần trước khi ghi sự cố.
public sealed class HealthProbeTests
{
    private static async Task<(HealthProbeResult Result, List<TimeSpan> Calls)> Run(params HealthProbeResult[] answers)
    {
        var calls = new List<TimeSpan>();
        var result = await HealthProbe.WithRetry((limit, _) => { calls.Add(limit); return Task.FromResult(answers[calls.Count - 1]); },
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), default);
        return (result, calls);
    }

    [Theory]
    [InlineData(null, "timeout")]
    [InlineData(null, "unreachable")]
    [InlineData(503, "dependency_unhealthy")]
    [InlineData(502, "dependency_unhealthy")]
    [InlineData(504, "dependency_unhealthy")]
    public async Task Cold_start_is_retried_with_longer_timeout(int? http, string error)
    {
        var (result, calls) = await Run(new("Unhealthy", http, error), new("Healthy", 200, null));
        Assert.Equal("Healthy", result.Status);
        Assert.Null(result.ErrorCode);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)], calls);
    }

    [Fact]
    public async Task Still_failing_after_retry_is_recorded()
    {
        var (result, calls) = await Run(new("Unhealthy", null, "timeout"), new("Unhealthy", null, "timeout"));
        Assert.Equal("timeout", result.ErrorCode);
        Assert.Equal(2, calls.Count);
    }

    [Theory]
    [InlineData("Healthy", 200, null)]
    [InlineData("Unhealthy", 200, null)]
    [InlineData("Unhealthy", 500, "dependency_unhealthy")]
    [InlineData("Unhealthy", 200, "invalid_response")]
    public async Task Real_answers_are_not_retried(string status, int http, string? error)
    {
        var (result, calls) = await Run(new HealthProbeResult(status, http, error));
        Assert.Single(calls);
        Assert.Equal(status, result.Status);
    }
}
