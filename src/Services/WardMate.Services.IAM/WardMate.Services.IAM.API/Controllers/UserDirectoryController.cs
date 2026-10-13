using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.API.Errors;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize]
public sealed class UserDirectoryController(ISender sender, IUserDirectory directory) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet("/api/v1/manager/officers"), Authorize(Policy = "iam.accounts.read")]
    [ProducesResponseType<DirectoryPage>(200)]
    public async Task<IActionResult> Officers([FromQuery] DirectoryFilter filter, CancellationToken ct)
    {
        var result = await sender.Send(new DirectoryQuery(Actor, false, filter), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }
    [HttpGet("/api/v1/admin/users"), Authorize(Policy = "iam.accounts.read")]
    [ProducesResponseType<DirectoryPage>(200)]
    public async Task<IActionResult> Users([FromQuery] DirectoryFilter filter, CancellationToken ct)
    {
        var result = await sender.Send(new DirectoryQuery(Actor, true, filter), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }
    public sealed record CategoriesInput(int[] Categories);
    [HttpPut("/api/v1/manager/officers/{userId:guid}/categories"), Authorize(Policy = "iam.accounts.manage")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Categories(Guid userId, CategoriesInput input, CancellationToken ct)
    {
        var result = await sender.Send(new AssignCategoriesCommand(Actor, userId, input.Categories), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
    [HttpGet("/api/v1/users/access-context")]
    [ProducesResponseType<AccessContextDto>(200)]
    public async Task<IActionResult> Access(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await directory.Access(Actor, ct);
        return result is null ? Unauthorized() : Ok(result);
    }
    [HttpGet("/api/v1/wards"), AllowAnonymous]
    [ProducesResponseType<WardDto[]>(200)]
    public async Task<IActionResult> Wards(CancellationToken ct) => Ok(await directory.Wards(ct));
}

