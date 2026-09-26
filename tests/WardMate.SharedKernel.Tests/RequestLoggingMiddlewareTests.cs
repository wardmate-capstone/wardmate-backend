using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WardMate.SharedKernel.Logging;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task RegularRequest_CompletesSuccessfullyThroughMiddleware()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        await using var app = builder.Build();
        app.UseWardMateRequestLoggingMiddleware();
        app.MapGet("/api/test", () => Results.Ok(new { message = "hello" }));
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthPath_SkipsWithoutFailure()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        await using var app = builder.Build();
        app.UseWardMateRequestLoggingMiddleware();
        app.MapGet("/health", () => Results.Ok("healthy"));
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NotFoundRequest_Returns404ThroughMiddleware()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        await using var app = builder.Build();
        app.UseWardMateRequestLoggingMiddleware();
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/non-existent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
