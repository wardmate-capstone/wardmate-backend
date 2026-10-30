using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WardMate.SharedKernel.Web;

// Runs after JWT authentication. Successful IAM answers are cached briefly (AccessContextCacheHandler),
// so a password change or revoked session blocks protected requests after at most Iam:AccessContextCacheSeconds.
public sealed class AccountSessionMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly HttpClient Client = new(new AccessContextCacheHandler(configuration)
        { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false } })
        { Timeout = TimeSpan.FromSeconds(5) };

    public async Task InvokeAsync(HttpContext context, IConfiguration config, ILogger<AccountSessionMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        var token = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(token) && context.Request.Path.StartsWithSegments("/hubs/notifications"))
            token = "Bearer " + context.Request.Query["access_token"].ToString();

        var status = 503;
        var valid = false;
        var target = "(invalid)";
        try
        {
            if (Uri.TryCreate(config["Iam:BaseUrl"], UriKind.Absolute, out var baseUri)
                && baseUri.Scheme is "https" or "http"
                && string.IsNullOrEmpty(baseUri.UserInfo)
                && string.IsNullOrEmpty(baseUri.Query)
                && string.IsNullOrEmpty(baseUri.Fragment))
            {
                // Log only the authority: never credentials, query strings, headers or response bodies.
                target = baseUri.GetLeftPart(UriPartial.Authority);
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/api/v1/users/access-context"));
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
                using var response = await Client.SendAsync(request, context.RequestAborted);
                valid = response.IsSuccessStatusCode;
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) status = 401;
                if (!valid)
                    logger.LogWarning("IAM session check failed: Reason={Reason} Target={Target} UpstreamStatus={UpstreamStatus} TraceId={TraceId}",
                        "upstream_response", target, (int)response.StatusCode, context.TraceIdentifier);
            }
            else
                logger.LogError("IAM session check failed: Reason={Reason} TraceId={TraceId}",
                    "invalid_base_url", context.TraceIdentifier);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or FormatException)
        {
            if (context.RequestAborted.IsCancellationRequested) throw;
            var reason = ex switch
            {
                HttpRequestException http => "transport_" + http.HttpRequestError,
                OperationCanceledException => "timeout",
                _ => "invalid_authorization_header"
            };
            // Do not log exception messages: HTTP exceptions can contain sensitive request details.
            logger.LogWarning("IAM session check failed: Reason={Reason} Target={Target} ExceptionType={ExceptionType} TraceId={TraceId}",
                reason, target, ex.GetType().Name, context.TraceIdentifier);
        }

        if (valid) { await next(context); return; }
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            status,
            title = status == 401 ? "Phiên đăng nhập đã hết hiệu lực." : "Không thể xác minh phiên đăng nhập lúc này.",
            code = status == 401 ? "auth.session_revoked" : "auth.session_unavailable",
            traceId = context.TraceIdentifier
        }, context.RequestAborted);
    }
}
