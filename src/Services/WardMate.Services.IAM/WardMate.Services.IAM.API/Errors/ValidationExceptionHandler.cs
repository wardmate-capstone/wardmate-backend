using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WardMate.Services.IAM.API.Errors;

public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not ValidationException validation) return false;
        var errors = validation.Errors.GroupBy(x => JsonNamingPolicy.CamelCase.ConvertName(x.PropertyName))
            .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).Distinct().ToArray());
        var problem = new ValidationProblemDetails(errors)
        {
            Status = 400,
            Title = "Validation failed.",
            Instance = context.Request.Path,
            Extensions = { ["code"] = "validation_failed", ["traceId"] = context.TraceIdentifier }
        };
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
