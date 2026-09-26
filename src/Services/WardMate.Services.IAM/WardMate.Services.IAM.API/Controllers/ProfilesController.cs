using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Profiles;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Domain;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize, Route("api/v1/users/me/profile")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ProfilesController(ISender sender) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet, Authorize(Policy = PermissionCodes.ProfileRead)]
    [ProducesResponseType<UserProfileDto>(200)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.Send(new GetProfileQuery(UserId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [HttpPost, Authorize(Policy = PermissionCodes.ProfileWrite)]
    [ProducesResponseType<UserProfileDto>(201)]
    public async Task<IActionResult> Create(ProfileInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateProfileCommand(UserId, input), ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), result.Value) : result.ToProblem(HttpContext);
    }

    [HttpPut, Authorize(Policy = PermissionCodes.ProfileWrite)]
    [ProducesResponseType<UserProfileDto>(200)]
    public async Task<IActionResult> Update(ProfileInput input, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateProfileCommand(UserId, input), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [HttpDelete, Authorize(Policy = PermissionCodes.ProfileWrite)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var result = await sender.Send(new DeleteProfileCommand(UserId), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}
