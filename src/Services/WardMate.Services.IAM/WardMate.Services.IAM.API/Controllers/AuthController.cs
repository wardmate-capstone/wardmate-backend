using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Commands;
using WardMate.Services.IAM.Application.DTOs;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
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
    [ProducesResponseType<AuthResponseDto>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Login(LoginCommand request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [HttpPost("refresh-token"), AllowAnonymous]
    [ProducesResponseType<AuthResponseDto>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Refresh(RefreshTokenCommand request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    public sealed record RevokeTokenRequest(string RefreshToken);
    [HttpPost("revoke-token"), Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    public async Task<IActionResult> Revoke(RevokeTokenRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        var result = await sender.Send(new RevokeTokenCommand(userId, request.RefreshToken), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}
