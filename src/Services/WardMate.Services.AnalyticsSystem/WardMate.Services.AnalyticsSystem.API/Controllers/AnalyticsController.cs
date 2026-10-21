using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.Services.AnalyticsSystem.API.Controllers;

[ApiController, Authorize, AnalyticsAccess("MANAGER"), Route("api/v1/analytics")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403), ProducesResponseType<ProblemDetails>(503)]
public sealed class AnalyticsController(IAnalyticsService service, TimeProvider clock) : ControllerBase
{
    private string Ward => ((NotificationAccess)HttpContext.Items["analytics.access"]!).WardCode!;
    private AnalyticsQuery Validate(AnalyticsQuery q)
    {
        AnalyticsDates.Validate(q.From, q.To, clock);
        if (q.ProcedureId == Guid.Empty) throw new AnalyticsException(400, "validation.failed", "ProcedureId không được là GUID rỗng.");
        return q;
    }
    [HttpGet("overview"), ProducesResponseType<DashboardOverview>(200)]
    public async Task<IActionResult> Overview([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await service.Overview(Ward, Validate(q), ct));
    [HttpGet("trends"), ProducesResponseType<TrendResult>(200)]
    public async Task<IActionResult> Trends([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await service.Trends(Ward, Validate(q), ct));
    [HttpGet("procedures"), ProducesResponseType<AnalyticsPage<ProcedureStatistics>>(200)]
    public async Task<IActionResult> Procedures([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await service.Procedures(Ward, Validate(q), ct));
    [HttpGet("officers"), ProducesResponseType<AnalyticsPage<OfficerStatistics>>(200)]
    public async Task<IActionResult> Officers([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await service.Officers(Ward, Validate(q), ct));
    [HttpGet("feedback/summary"), ProducesResponseType<FeedbackSummary>(200)]
    public async Task<IActionResult> FeedbackSummary([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await service.FeedbackSummary(Ward, Validate(q), ct));
    [HttpGet("feedback"), ProducesResponseType<AnalyticsPage<FeedbackDto>>(200)]
    public async Task<IActionResult> Feedback([FromQuery] AnalyticsQuery q, CancellationToken ct) => Ok(await service.FeedbackList(Ward, Validate(q), ct));
}

public sealed record RebuildMetrics(DateOnly From, DateOnly To);
public sealed record RebuildAccepted(string JobId);
public sealed record AnalyticsJobStatus(string JobId, string State, DateTime CreatedAtUtc);
[ApiController, Authorize, AnalyticsAccess("IT_ADMIN"), Route("api/v1/analytics/jobs")]
public sealed class AnalyticsJobsController(IConfiguration config, TimeProvider clock) : ControllerBase
{
    [HttpGet("{jobId}"), ProducesResponseType<AnalyticsJobStatus>(200)]
    public IActionResult Status(string jobId)
    {
        if (!config.GetValue("Analytics:JobsEnabled", false))
            throw new AnalyticsException(503, "analytics.jobs_disabled", "Job thống kê chưa được bật.");
        if (jobId.Length > 30 || !jobId.All(char.IsAsciiDigit))
            throw new AnalyticsException(400, "validation.job_id", "JobId không hợp lệ.");
        using var connection = HttpContext.RequestServices.GetRequiredService<JobStorage>().GetConnection();
        var data = connection.GetJobData(jobId);
        if (data is null || data.Job?.Type != typeof(AnalyticsAggregationJob))
            throw new AnalyticsException(404, "analytics.job_not_found", "Không tìm thấy job thống kê.");
        return Ok(new AnalyticsJobStatus(jobId, data.State, DateTime.SpecifyKind(data.CreatedAt, DateTimeKind.Utc)));
    }
    [HttpPost("rebuild"), ProducesResponseType<RebuildAccepted>(202)]
    public IActionResult Rebuild(RebuildMetrics input)
    {
        AnalyticsDates.Validate(input.From, input.To, clock);
        if (!config.GetValue("Analytics:JobsEnabled", false))
            throw new AnalyticsException(503, "analytics.jobs_disabled", "Job thống kê chưa được bật.");
        var id = HttpContext.RequestServices.GetRequiredService<IBackgroundJobClient>()
            .Enqueue<AnalyticsAggregationJob>(job => job.Rebuild(input.From, input.To, CancellationToken.None));
        return Accepted(new RebuildAccepted(id));
    }
}
internal static class AnalyticsDates
{
    public static void Validate(DateOnly from, DateOnly to, TimeProvider clock)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (from < new DateOnly(2000, 1, 1) || from > to || to > today || to.DayNumber - from.DayNumber >= 366)
            throw new AnalyticsException(400, "validation.date_range", "Cần From/To dạng yyyy-MM-dd, từ 1 đến 366 ngày, không nằm trong tương lai.");
    }
}
