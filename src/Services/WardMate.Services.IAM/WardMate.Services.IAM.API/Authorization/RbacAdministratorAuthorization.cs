using Microsoft.AspNetCore.Authorization;
using WardMate.Services.IAM.Application.Rbac;

namespace WardMate.Services.IAM.API.Authorization;

public sealed class RbacAdministratorRequirement : IAuthorizationRequirement
{
    public const string Policy = "iam.rbac.admin";
}

public sealed class RbacAdministratorHandler(IRbacStore store) : AuthorizationHandler<RbacAdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RbacAdministratorRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var actor)) return;
        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        if (await store.IsAdministrator(actor, ct)) context.Succeed(requirement);
    }
}
