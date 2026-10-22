using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
namespace WardMate.SharedKernel.Web;
// Runs after JWT authentication. No cache: password changes invalidate the next protected request.
public sealed class AccountSessionMiddleware(RequestDelegate next)
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        var token = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(token) && context.Request.Path.StartsWithSegments("/hubs/notifications"))
            token = "Bearer " + context.Request.Query["access_token"].ToString();
        var status = 503;
        var valid = false;
        try
        {
            if (Uri.TryCreate(config["Iam:BaseUrl"], UriKind.Absolute, out var baseUri) && baseUri.Scheme is "https" or "http")
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/api/v1/users/access-context"));
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
                using var response = await Client.SendAsync(request, context.RequestAborted);
                valid = response.IsSuccessStatusCode;
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) status = 401;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or FormatException) { if (context.RequestAborted.IsCancellationRequested) throw; }
        if (valid) { await next(context); return; }
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { status, title = status == 401 ? "Phiên đăng nhập đã hết hiệu lực." : "Không thể xác minh phiên đăng nhập lúc này.", code = status == 401 ? "auth.session_revoked" : "auth.session_unavailable" }, context.RequestAborted);
    }
}
