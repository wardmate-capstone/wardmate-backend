using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Queries;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize, Route("api/v1/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet("me"), Authorize(Policy = WardMate.Services.IAM.Domain.PermissionCodes.ProfileRead)]
    [ProducesResponseType<CurrentUserDto>(200)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await sender.Send(new GetCurrentUserQuery(Guid.Parse(User.FindFirstValue("sub")!)), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }
}
