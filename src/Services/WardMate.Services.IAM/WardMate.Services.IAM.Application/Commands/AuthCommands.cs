using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;

namespace WardMate.Services.IAM.Application.Commands;

public sealed record RegisterCitizenCommand(string Username, string Email, string Password, string FullName) : IRequest<Result<CurrentUserDto>>;
public sealed record LoginCommand(string UsernameOrEmail, string Password) : IRequest<Result<AuthResponseDto>>;
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponseDto>>;
// UserId is supplied by the authenticated controller, never taken from the request body.
public sealed record RevokeTokenCommand(Guid UserId, string RefreshToken) : IRequest<Result<bool>>;
