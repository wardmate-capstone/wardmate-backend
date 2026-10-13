using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.API;

// Resolve current permissions and ward from IAM. Do not trust a caller-supplied ward or stale token ward.
public sealed class WorkflowAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IWorkflowDirectory directory)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                var token = context.Request.Headers.Authorization.ToString();
                var access = await directory.Access(token["Bearer ".Length..], context.RequestAborted);
                if (access is null || access.UserId.ToString() != context.User.FindFirstValue("sub"))
                { await Error(context, 401, "auth.unauthorized", "Tài khoản không khả dụng."); return; }
                if (access.Roles is null || access.Permissions is null)
                { await Error(context, 503, "application.iam_unavailable", "Ngữ cảnh quyền IAM không hợp lệ."); return; }
                var identity = (ClaimsIdentity)context.User.Identity;
                foreach (var claim in identity.Claims.Where(c => c.Type is "permissions" or "ward_code" or "role").ToArray()) identity.RemoveClaim(claim);
                identity.AddClaims(access.Permissions.Select(p => new Claim("permissions", p)));
                identity.AddClaims(access.Roles.Select(r => new Claim("role", r)));
                if (!string.IsNullOrWhiteSpace(access.WardCode)) identity.AddClaim(new Claim("ward_code", access.WardCode));
            }
            catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
            { await Error(context, 503, "application.iam_unavailable", "Chưa thể xác minh quyền/phường từ IAM. Vui lòng thử lại."); return; }
        }
        await next(context);
    }
    private static Task Error(HttpContext context, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title,
            Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier } },
            options: null, contentType: "application/problem+json");
    }
}

