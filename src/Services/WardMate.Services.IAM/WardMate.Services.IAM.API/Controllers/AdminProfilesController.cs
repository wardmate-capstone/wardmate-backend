using System.Security.Claims;
using WardMate.Services.IAM.API.Authorization;
using WardMate.Services.IAM.Application.Accounts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Profiles;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Domain;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize(Policy = AccountManagementRequirement.Policy), Route("api/v1/users/{userId:guid}/profile")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class AdminProfilesController(ISender sender, IManagementScope scope) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserProfileDto>(200)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var result = await scope.Execute(Guid.Parse(User.FindFirstValue("sub")!), userId, () => sender.Send(new GetProfileQuery(userId), ct), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [HttpPost]
    [ProducesResponseType<UserProfileDto>(201)]
    public async Task<IActionResult> Create(Guid userId, ProfileInput input, CancellationToken ct)
    {
        var result = await scope.Execute(Guid.Parse(User.FindFirstValue("sub")!), userId, () => sender.Send(new CreateProfileCommand(userId, input), ct), ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { userId }, result.Value) : result.ToProblem(HttpContext);
    }

    [HttpPut]
    [ProducesResponseType<UserProfileDto>(200)]
    public async Task<IActionResult> Update(Guid userId, ProfileInput input, CancellationToken ct)
    {
        var result = await scope.Execute(Guid.Parse(User.FindFirstValue("sub")!), userId, () => sender.Send(new UpdateProfileCommand(userId, input), ct), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken ct)
    {
        var result = await scope.Execute(Guid.Parse(User.FindFirstValue("sub")!), userId, () => sender.Send(new DeleteProfileCommand(userId), ct), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}
