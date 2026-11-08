using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;
namespace WardMate.Services.ApplicationWorkflow.API.Controllers;

[Route("api/v1/staffing/closures")]
public sealed class CalendarClosuresController(ICalendarClosureService service) : ReviewControllerBase
{
    private CalendarActor CalendarUser => new(Guid.Parse(User.FindFirstValue("sub")!),User.FindFirstValue("ward_code"),
        (User.IsInRole("MANAGER") || User.IsInRole("FRONT_DESK_OFFICER")) && User.HasClaim("permissions","workflow.staffing.read"),
        User.IsInRole("MANAGER") && User.HasClaim("permissions","workflow.staffing.manage"),
        User.IsInRole("IT_ADMIN") && User.HasClaim("permissions","workflow.calendar.manage"), User.FindFirstValue("officer_name"));
    [HttpGet, Authorize(Policy="workflow.calendar.read")]
    public async Task<IActionResult> List([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) => Respond(await service.List(CalendarUser,from,to,ct));
    [HttpPost, Authorize(Policy="workflow.staffing.manage")]
    public async Task<IActionResult> Create(ClosureInput input,CancellationToken ct) => Respond(await service.Create(CalendarUser,input,false,ct));
    [HttpPost("national"), Authorize(Policy="workflow.calendar.manage")]
    public async Task<IActionResult> National(ClosureInput input,CancellationToken ct) => Respond(await service.Create(CalendarUser,input,true,ct));
    [HttpPost("{id:guid}/cancel"), Authorize(Policy="workflow.calendar.cancel")]
    public async Task<IActionResult> Cancel(Guid id,ClosureCancelInput input,CancellationToken ct) => Respond(await service.Cancel(CalendarUser,id,input,ct));
}
