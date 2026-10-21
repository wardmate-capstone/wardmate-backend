using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Queries;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.API.Authorization;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize, Route("api/v1/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    public sealed record ChangePasswordInput(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

    [HttpPost("me/change-password"), RequestSizeLimit(4096)]
    [ProducesResponseType(200), ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input, CancellationToken ct)
    {
        var result = await sender.Send(new WardMate.Services.IAM.Application.Commands.ChangePasswordCommand(
            Guid.Parse(User.FindFirstValue("sub")!), input.CurrentPassword, input.NewPassword, input.ConfirmNewPassword), ct);
        return result.IsSuccess ? Ok(new { message = result.Value }) : result.ToProblem(HttpContext);
    }

    [HttpGet, Authorize(Policy = AccountManagementRequirement.Policy), Authorize(Policy = "iam.accounts.read")]
    [ProducesResponseType<ManagedUserPage>(200)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await sender.Send(new ListUsersQuery(Guid.Parse(User.FindFirstValue("sub")!), page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [HttpGet("me"), Authorize(Policy = WardMate.Services.IAM.Domain.PermissionCodes.ProfileRead)]
    [ProducesResponseType<CurrentUserDto>(200)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await sender.Send(new GetCurrentUserQuery(Guid.Parse(User.FindFirstValue("sub")!)), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }
}


