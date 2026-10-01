using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Cors.Infrastructure;
using WardMate.SharedKernel.Web;

namespace WardMate.Services.IAM.API.Security;

// A custom header forces browser cross-origin requests through a CORS preflight.
// Pair with the exact-origin credentialed CORS policy on both IAM and Gateway.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BrowserAuthProtectionAttribute : Attribute, IAsyncResourceFilter
{
    public const string HeaderName = "X-CSRF-Protection";
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.Pragma = "no-cache";
        var request = context.HttpContext.Request;
        var origin = request.Headers.Origin.ToString();
        var policy = await context.HttpContext.RequestServices.GetRequiredService<ICorsPolicyProvider>()
            .GetPolicyAsync(context.HttpContext, BrowserCorsExtensions.PolicyName);
        var sameOrigin = string.Equals(origin, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase);
        var deniedOrigin = origin.Length > 0 && !sameOrigin && policy?.IsOriginAllowed(origin) != true;
        if (request.Headers[HeaderName] != "1" || deniedOrigin)
        {
            var problem = new ProblemDetails
            {
                Status = 403, Title = "Yêu cầu bị từ chối do nguồn truy cập hoặc header bảo vệ CSRF không hợp lệ.", Instance = context.HttpContext.Request.Path,
                Extensions = { ["code"] = "iam.csrf_rejected", ["traceId"] = context.HttpContext.TraceIdentifier }
            };
            var response = new ObjectResult(problem) { StatusCode = 403 };
            response.ContentTypes.Add("application/problem+json");
            context.Result = response;
            return;
        }
        await next();
    }
}
