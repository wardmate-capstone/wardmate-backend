using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/staffing"), Authorize(Policy="workflow.staffing.read")]
public sealed class MonthlySchedulesController(IMonthlySchedules schedules) : ReviewControllerBase
{
    private async Task<IActionResult> Run(Func<Task<WorkflowResult<object>>> action)
    {
        try { return Respond(await action()); }
        catch(Exception e) when(e is HttpRequestException or System.Text.Json.JsonException ||
            e is OperationCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        { return Respond(WorkflowResult<object>.Fail(503,"schedule.iam_unavailable","Chưa lấy được danh sách cán bộ từ IAM.")); }
    }
    [HttpGet("schedules/{month}")]
    [ProducesResponseType(typeof(MonthlyView),200)]
    public Task<IActionResult> Month(DateOnly month,CancellationToken ct)=>Run(()=>schedules.Month(Actor,month,ct));
    [HttpGet("week-pattern"),Authorize(Policy="workflow.staffing.manage")]
    [ProducesResponseType(typeof(WeekView),200)]
    public Task<IActionResult> Week(CancellationToken ct)=>Run(()=>schedules.Week(Actor,null,ct));
    [HttpPut("week-pattern"),Authorize(Policy="workflow.staffing.manage")]
    [ProducesResponseType(typeof(WeekView),200)]
    public Task<IActionResult> SaveWeek(WeekEdit body,CancellationToken ct)=>Run(()=>schedules.Week(Actor,body,ct));
    [HttpGet("schedules/{month}/preview"),Authorize(Policy="workflow.staffing.manage")]
    [ProducesResponseType(typeof(PreviewView),200)]
    public Task<IActionResult> Preview(DateOnly month,DateOnly? copyFrom,CancellationToken ct)=>Run(()=>schedules.Preview(Actor,month,copyFrom,ct));
    [HttpPut("schedules/{month}/draft"),Authorize(Policy="workflow.staffing.manage")]
    [ProducesResponseType(typeof(MonthlyRevisionView),200)]
    public Task<IActionResult> Draft(DateOnly month,ScheduleDraftInput body,CancellationToken ct)=>Run(()=>schedules.Draft(Actor,month,body,ct));
    [HttpPost("schedules/{month}/publish"),Authorize(Policy="workflow.staffing.manage")]
    [ProducesResponseType(typeof(MonthlyRevisionView),200)]
    public Task<IActionResult> Publish(DateOnly month,PublishSchedule body,CancellationToken ct)=>
        Run(()=>schedules.Publish(Actor,month,body,Request.Headers.Authorization.ToString()[7..],ct));
    [HttpGet("schedules/{month}/history"),Authorize(Policy="workflow.staffing.manage")]
    [ProducesResponseType(typeof(MonthlyRevisionView[]),200)]
    public Task<IActionResult> History(DateOnly month,CancellationToken ct)=>Run(()=>schedules.History(Actor,month,ct));
    [HttpPost("schedules/{month}/confirm"),Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(WardMate.Services.ApplicationWorkflow.Domain.OfficerScheduleReceipt),200)]
    public Task<IActionResult> Confirm(DateOnly month,ScheduleConfirmation body,CancellationToken ct)=>Run(()=>schedules.Confirm(Actor,month,body,ct));
    [HttpGet("shift-change-requests")]
    [ProducesResponseType(typeof(ChangePage),200)]
    public Task<IActionResult> Changes(CancellationToken ct,int page=1,int pageSize=20)=>Run(()=>schedules.Changes(Actor,page,pageSize,ct));
    [HttpPost("shift-change-requests"),Authorize(Policy="workflow.leave.request")]
    [ProducesResponseType(typeof(WardMate.Services.ApplicationWorkflow.Domain.ShiftChangeRequest),200)]
    public Task<IActionResult> RequestChange(ChangeShiftInput body,CancellationToken ct)=>Run(()=>schedules.RequestChange(Actor,body,ct));
    [HttpPost("shift-change-requests/{id:guid}/decision"),Authorize(Policy="workflow.staffing.manage")]
    [ProducesResponseType(typeof(WardMate.Services.ApplicationWorkflow.Domain.ShiftChangeRequest),200)]
    public Task<IActionResult> DecideChange(Guid id,DecideShift body,CancellationToken ct)=>
        Run(()=>schedules.DecideChange(Actor,id,body,Request.Headers.Authorization.ToString()[7..],ct));
}
