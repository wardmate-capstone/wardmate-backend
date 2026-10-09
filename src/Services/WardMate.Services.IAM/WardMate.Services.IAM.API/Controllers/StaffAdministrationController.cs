using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Authorization;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Accounts;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize(Policy = AccountManagementRequirement.Policy), Route("api/v1/accounts")]
public sealed class StaffAdministrationController(ISender sender) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpPost("front-desk"), Authorize(Policy = "iam.accounts.manage")]
    [ProducesResponseType<ManagedUserDto>(201)]
    public async Task<IActionResult> Create(CreateFrontDeskInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateFrontDeskCommand(ActorId, input), ct);
        return result.IsSuccess ? Created($"/api/v1/accounts/{result.Value!.Id}", result.Value) : result.ToProblem(HttpContext);
    }

    [HttpGet("wards"), Authorize(Policy = "iam.wards.read")]
    [ProducesResponseType<WardDto[]>(200)]
    public async Task<IActionResult> Wards(CancellationToken ct)
    {
        var result = await sender.Send(new ListWardsQuery(ActorId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    public sealed record WardInput(string Code, string Name);
    [HttpPost("wards"), Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.wards.manage")]
    [ProducesResponseType<WardDto>(201)]
    public async Task<IActionResult> CreateWard(WardInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateWardCommand(ActorId, input.Code, input.Name), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.ToProblem(HttpContext);
    }

    public sealed record AssignWardInput(Guid? WardId);
    [HttpPut("{userId:guid}/ward"), Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.wards.manage")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Assign(Guid userId, AssignWardInput input, CancellationToken ct)
    {
        var result = await sender.Send(new AssignWardCommand(ActorId, userId, input.WardId), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}

