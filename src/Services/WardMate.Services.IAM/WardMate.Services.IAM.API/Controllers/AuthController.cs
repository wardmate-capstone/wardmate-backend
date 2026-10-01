using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.API.Security;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Route("api/v1/auth")]
[BrowserAuthProtection]
public sealed class AuthController(ISender sender, RefreshTokenCookie cookie) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous]
    [ProducesResponseType<CurrentUserDto>(201)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Register(RegisterCitizenCommand request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        return result.IsSuccess ? Created("/api/v1/users/me", result.Value) : result.ToProblem(HttpContext);
    }

    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType<BrowserAuthResponse>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Login(LoginCommand request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        if (!result.IsSuccess) return result.ToProblem(HttpContext);
        cookie.Write(HttpContext, result.Value!);
        return Ok(BrowserAuthResponse.From(result.Value!));
    }

    [HttpPost("refresh-token"), AllowAnonymous]
    [ProducesResponseType<BrowserAuthResponse>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refresh = cookie.Read(HttpContext);
        if (string.IsNullOrWhiteSpace(refresh)) return Result<AuthResponseDto>.Failure(AuthErrors.InvalidToken).ToProblem(HttpContext);
        var result = await sender.Send(new RefreshTokenCommand(refresh), ct);
        if (!result.IsSuccess) return result.ToProblem(HttpContext);
        cookie.Write(HttpContext, result.Value!);
        return Ok(BrowserAuthResponse.From(result.Value!));
    }

    [HttpPost("revoke-token"), Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        var refresh = cookie.Read(HttpContext);
        if (string.IsNullOrWhiteSpace(refresh))
        {
            cookie.Delete(HttpContext);
            return NoContent();
        }
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        var result = await sender.Send(new RevokeTokenCommand(userId, refresh), ct);
        if (!result.IsSuccess) return result.ToProblem(HttpContext);
        cookie.Delete(HttpContext);
        return NoContent();
    }
}
