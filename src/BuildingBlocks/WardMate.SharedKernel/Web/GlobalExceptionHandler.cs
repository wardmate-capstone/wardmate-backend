using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WardMate.SharedKernel.Web;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        logger.LogError(exception, "Unhandled request exception. TraceId: {TraceId}", traceId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
            Instance = httpContext.Request.Path,
            Extensions = { ["traceId"] = traceId }
        };

        httpContext.Response.StatusCode = problem.Status.Value;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });

        // Always return a sanitized error body, even for unsupported Accept headers.
        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        }

        return true;
    }
}
