using Microsoft.AspNetCore.Authorization;
using WardMate.Services.IAM.Application.Accounts;

namespace WardMate.Services.IAM.API.Authorization;

public sealed record AccountManagementRequirement : IAuthorizationRequirement
{
    public const string Policy = "AccountManagement";
}

public sealed class AccountManagementHandler(IManagementScope scope) : AuthorizationHandler<AccountManagementRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AccountManagementRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id)) return;
        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        if (await scope.CanManage(id, ct)) context.Succeed(requirement);
    }
}
