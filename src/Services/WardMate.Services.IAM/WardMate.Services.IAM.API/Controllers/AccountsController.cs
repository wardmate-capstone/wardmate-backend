using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Domain;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize(Policy = PermissionCodes.Manage), Route("api/v1/accounts")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class AccountsController(ISender sender) : ControllerBase
{
    [HttpGet, ProducesResponseType<AccountPage>(200)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await sender.Send(new ListAccountsQuery(page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<AccountDto>(200)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetAccountQuery(userId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    public sealed record AccountStatusInput(bool? IsActive);
    [HttpPut("{userId:guid}/status")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Status(Guid userId, AccountStatusInput input, CancellationToken ct)
    {
        if (input.IsActive is null)
            throw new FluentValidation.ValidationException([new("IsActive", "Trạng thái tài khoản không được để trống.")]);
        var result = await sender.Send(new SetAccountStatusCommand(Guid.Parse(User.FindFirstValue("sub")!), userId, input.IsActive.Value), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}
