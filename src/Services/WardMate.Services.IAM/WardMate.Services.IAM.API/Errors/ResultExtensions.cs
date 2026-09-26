using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.Application.Common;

namespace WardMate.Services.IAM.API.Errors;

public static class ResultExtensions
{
    public static IActionResult ToProblem<T>(this Result<T> result, HttpContext context)
    {
        var error = result.Error ?? throw new InvalidOperationException("Cannot map a successful result to an error.");
        var status = error.Kind switch { ErrorKind.Conflict => 409, ErrorKind.Unauthorized => 401, ErrorKind.NotFound => 404, ErrorKind.Forbidden => 403, _ => 500 };
        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Message,
            Instance = context.Request.Path,
            Extensions = { ["code"] = error.Code, ["traceId"] = context.TraceIdentifier }
        };
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
