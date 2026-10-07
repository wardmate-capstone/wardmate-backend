using Microsoft.AspNetCore.Authorization;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain;

namespace WardMate.Services.IAM.API.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

// Uses the current role-permission assignments rather than stale JWT claims.
public sealed class PermissionAuthorizationHandler(IIdentityStore store) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId)) return;
        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var user = await store.FindUser(userId, ct);
        if (user is { IsActive: true } && user.UserRoles.SelectMany(x => x.Role.RolePermissions)
            .Any(x => x.Permission.PermissionCode == requirement.Permission)) context.Succeed(requirement);
    }
}

public static class PermissionAuthorization
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, RbacAdministratorHandler>();
        services.AddScoped<IAuthorizationHandler, AccountManagementHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AccountManagementRequirement.Policy, policy => policy.RequireAuthenticatedUser().AddRequirements(new AccountManagementRequirement()));
            options.AddPolicy(RbacAdministratorRequirement.Policy, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new RbacAdministratorRequirement()));
            foreach (var permission in new[] { PermissionCodes.ProfileRead, PermissionCodes.ProfileWrite, PermissionCodes.Manage })
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
        });
        return services;
    }
}
