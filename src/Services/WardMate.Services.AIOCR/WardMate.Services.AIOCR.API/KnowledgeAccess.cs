using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WardMate.Services.AIOCR.API;

// Knowledge management needs current IAM role + permission; stale JWT claims are not authority.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class KnowledgeAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string Permission = "knowledge.manage";
    private static readonly string[] Roles = ["PROCEDURE_MANAGER", "IT_ADMIN"];

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true) { Fail(401, "auth.unauthorized", "Vui lòng đăng nhập."); return; }
        var bearer = http.Request.Headers.Authorization.ToString();
        if (!bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) { Fail(401, "auth.unauthorized", "Thiếu Bearer token."); return; }
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        if (!Uri.TryCreate(config["Iam:BaseUrl"], UriKind.Absolute, out var iam) || iam.Scheme is not ("https" or "http"))
        { Fail(503, "knowledge.iam_unavailable", "Chưa cấu hình Iam:BaseUrl."); return; }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(iam, "api/v1/users/access-context"));
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(bearer);
            using var response = await http.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("knowledge-iam").SendAsync(request, http.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { Fail(401, "auth.session_revoked", "Phiên đăng nhập không còn hiệu lực."); return; }
            response.EnsureSuccessStatusCode();
            var access = await response.Content.ReadFromJsonAsync<AccessContext>(cancellationToken: http.RequestAborted);
            if (access is null || access.UserId.ToString() != http.User.FindFirstValue("sub"))
            { Fail(401, "auth.unauthorized", "Tài khoản không còn khả dụng."); return; }
            if (access.Roles?.Any(Roles.Contains) != true || access.Permissions?.Contains(Permission) != true)
            { Fail(403, "knowledge.forbidden", "Cần vai trò PROCEDURE_MANAGER hoặc IT_ADMIN và quyền knowledge.manage."); return; }
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !http.RequestAborted.IsCancellationRequested)
        { Fail(503, "knowledge.iam_unavailable", "Chưa xác minh được quyền từ IAM. Vui lòng thử lại."); }

        void Fail(int status, string code, string message) => context.Result = new ObjectResult(new ProblemDetails
        { Status = status, Title = message, Instance = http.Request.Path, Extensions = { ["code"] = code, ["traceId"] = http.TraceIdentifier } })
        { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    private sealed record AccessContext(Guid UserId, string[]? Roles, string[]? Permissions);
}
