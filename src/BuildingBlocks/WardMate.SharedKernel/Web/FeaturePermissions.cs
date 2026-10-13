using Microsoft.Extensions.DependencyInjection;

namespace WardMate.SharedKernel.Web;

public static class FeaturePermissions
{
    public static readonly string[] Procedure = ["procedure.read", "procedure.create", "procedure.update", "procedure.publish", "procedure.status",
        "procedure.versions.read", "procedure.rollback", "procedure.source.read", "procedure.categories.manage", "procedure.drafts.read",
        "procedure.drafts.upload", "procedure.drafts.update", "procedure.drafts.extract", "procedure.drafts.publish", "procedure.drafts.delete"];
    public static readonly string[] Workflow = ["workflow.create", "workflow.read", "workflow.checklist.write", "workflow.submit", "workflow.resubmit",
        "workflow.queue.read", "workflow.assign", "workflow.comments.write", "workflow.revision.request", "workflow.approve", "workflow.reject",
        "workflow.comments.read", "workflow.versions.read", "workflow.diff.read"];
    public static readonly string[] Iam = ["iam.accounts.read", "iam.accounts.manage", "iam.wards.read", "iam.wards.manage", "iam.rbac.manage", "iam.audit.read"];
    public static IServiceCollection AddClaimPermissions(this IServiceCollection services, IEnumerable<string> codes)
    {
        services.AddAuthorization(options =>
        {
            foreach (var code in codes) options.AddPolicy(code, p => p.RequireAuthenticatedUser().RequireClaim("permissions", code));
        });
        return services;
    }
}
