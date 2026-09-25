using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using WardMate.SharedKernel.Web;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData("Production", "application/json")]
    [InlineData("Production", "text/html")]
    [InlineData("Development", "application/json")]
    public async Task UnhandledExceptionReturnsSanitizedProblemDetails(string environment, string accept)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddGlobalExceptionHandling();

        await using var app = builder.Build();
        app.UseGlobalExceptionHandling();
        // This failing endpoint exists only in the test host.
        app.MapGet("/throw", ThrowSensitiveException);
        await app.StartAsync();

        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.GetAsync("/throw");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("private-database-password", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/throw", problem.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    private static string ThrowSensitiveException()
        => throw new InvalidOperationException("private-database-password");
}
