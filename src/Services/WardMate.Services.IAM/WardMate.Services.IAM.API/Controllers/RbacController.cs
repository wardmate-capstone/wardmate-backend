using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Authorization;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Rbac;

namespace WardMate.Services.IAM.API.Controllers;

[ApiController, Authorize, Route("api/v1/rbac")]
[ProducesResponseType<ValidationProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class RbacController(ISender sender) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue("sub")!);

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpGet("roles"), ProducesResponseType<RbacPage<RoleDto>>(200)]
    public async Task<IActionResult> Roles([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await sender.Send(new ListRolesQuery(page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpGet("roles/{roleId:int}"), ProducesResponseType<RoleDto>(200)]
    public async Task<IActionResult> Role(int roleId, CancellationToken ct)
    {
        var result = await sender.Send(new GetRoleQuery(roleId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpPost("roles"), ProducesResponseType<RoleDto>(201)]
    public async Task<IActionResult> CreateRole(RoleInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateRoleCommand(ActorId, input), ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Role), new { roleId = result.Value!.Id }, result.Value) : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpPut("roles/{roleId:int}"), ProducesResponseType<RoleDto>(200)]
    public async Task<IActionResult> UpdateRole(int roleId, RoleInput input, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateRoleCommand(ActorId, roleId, input), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpDelete("roles/{roleId:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> DeleteRole(int roleId, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteRoleCommand(ActorId, roleId), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpGet("permissions"), ProducesResponseType<PermissionDto[]>(200)]
    public async Task<IActionResult> Permissions(CancellationToken ct)
    {
        var result = await sender.Send(new ListPermissionsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpGet("roles/{roleId:int}/permissions"), ProducesResponseType<PermissionDto[]>(200)]
    public async Task<IActionResult> RolePermissions(int roleId, CancellationToken ct)
    {
        var result = await sender.Send(new GetRoleQuery(roleId), ct);
        return result.IsSuccess ? Ok(result.Value!.Permissions) : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpPut("roles/{roleId:int}/permissions/{permissionId:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> GrantPermission(int roleId, int permissionId, CancellationToken ct)
    {
        var result = await sender.Send(new SetRolePermissionCommand(ActorId, roleId, permissionId, true), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpDelete("roles/{roleId:int}/permissions/{permissionId:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> RevokePermission(int roleId, int permissionId, CancellationToken ct)
    {
        var result = await sender.Send(new SetRolePermissionCommand(ActorId, roleId, permissionId, false), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.rbac.manage")]
    [HttpGet("users/{userId:guid}/roles"), ProducesResponseType<RoleDto[]>(200)]
    public async Task<IActionResult> UserRoles(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserRolesQuery(userId), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = AccountManagementRequirement.Policy), Authorize(Policy = "iam.accounts.manage")]
    [HttpPut("users/{userId:guid}/roles/{roleId:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> GrantRole(Guid userId, int roleId, CancellationToken ct)
    {
        var result = await sender.Send(new SetUserRoleCommand(ActorId, userId, roleId, true), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = AccountManagementRequirement.Policy), Authorize(Policy = "iam.accounts.manage")]
    [HttpDelete("users/{userId:guid}/roles/{roleId:int}"), ProducesResponseType(204)]
    public async Task<IActionResult> RevokeRole(Guid userId, int roleId, CancellationToken ct)
    {
        var result = await sender.Send(new SetUserRoleCommand(ActorId, userId, roleId, false), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    [Authorize(Policy = RbacAdministratorRequirement.Policy), Authorize(Policy = "iam.audit.read")]
    [HttpGet("audit-logs"), ProducesResponseType<RbacPage<AuditDto>>(200)]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await sender.Send(new ListRbacAuditQuery(page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }
}


