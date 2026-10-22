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
    [HttpGet("me/phone-verification")]
    public async Task<IActionResult> PhoneStatus([FromServices] WardMate.Services.IAM.Application.Interfaces.IPhoneVerificationService service, CancellationToken ct)
    {
        var result = await service.Status(Guid.Parse(User.FindFirstValue("sub")!), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }
    public sealed record PhoneRequest(string PhoneNumber, string CurrentPassword);
    public sealed record PhoneConfirmation(string Otp);
    [HttpPost("me/phone-verification/request"), RequestSizeLimit(4096)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("password-otp")]
    public async Task<IActionResult> RequestPhone(PhoneRequest input, [FromServices] WardMate.Services.IAM.Application.Interfaces.IPhoneVerificationService service, CancellationToken ct)
    {
        var result = await service.Request(Guid.Parse(User.FindFirstValue("sub")!), input.PhoneNumber, input.CurrentPassword, ct);
        return result.IsSuccess ? Accepted(new { message = result.Value }) : result.ToProblem(HttpContext);
    }
    [HttpPost("me/phone-verification/confirm"), RequestSizeLimit(4096)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("password-otp")]
    public async Task<IActionResult> ConfirmPhone(PhoneConfirmation input, [FromServices] WardMate.Services.IAM.Application.Interfaces.IPhoneVerificationService service, CancellationToken ct)
    {
        var result = await service.Verify(Guid.Parse(User.FindFirstValue("sub")!), input.Otp, ct);
        return result.IsSuccess ? Ok(new { message = result.Value }) : result.ToProblem(HttpContext);
    }
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


