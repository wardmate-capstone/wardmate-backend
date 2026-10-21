using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.API;

// Resolve current IAM role/ward on every request; client query and stale JWT ward are never authority.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AnalyticsAccessAttribute(string role) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true) { Fail(401, "auth.unauthorized", "Vui lòng đăng nhập."); return; }
        try
        {
            var authorization = http.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) { Fail(401, "auth.unauthorized", "Thiếu Bearer token."); return; }
            var access = await http.RequestServices.GetRequiredService<INotificationDirectory>()
                .GetAsync(authorization[7..], http.RequestAborted);
            if (access is null || !Guid.TryParse(http.User.FindFirstValue("sub"), out var userId) || access.UserId != userId)
            { Fail(401, "auth.unauthorized", "Tài khoản không còn khả dụng."); return; }
            if (access.Roles is null) { Fail(503, "analytics.iam_unavailable", "IAM trả ngữ cảnh không hợp lệ."); return; }
            if (!access.Roles.Contains(role) || role == "MANAGER" && string.IsNullOrWhiteSpace(access.WardCode))
            { Fail(403, "analytics.forbidden", "Không có vai trò hoặc phạm vi phường phù hợp."); return; }
            http.Items["analytics.access"] = access;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !http.RequestAborted.IsCancellationRequested)
        { Fail(503, "analytics.iam_unavailable", "Chưa thể xác minh quyền từ IAM."); }
        void Fail(int status, string code, string message) => context.Result = new ObjectResult(new ProblemDetails
        { Status = status, Title = message, Extensions = { ["code"] = code, ["traceId"] = http.TraceIdentifier } })
        { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
public sealed class AnalyticsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AnalyticsException e) return false;
        context.Response.StatusCode = e.Status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = e.Status, Title = e.Message,
            Instance = context.Request.Path, Extensions = { ["code"] = e.Code, ["traceId"] = context.TraceIdentifier } },
            options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
