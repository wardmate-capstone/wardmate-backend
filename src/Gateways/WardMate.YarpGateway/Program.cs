using Microsoft.AspNetCore.HttpOverrides;
using WardMate.SharedKernel.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// The cloud ingress appends the caller to X-Forwarded-For; trust that one hop so YARP forwards the real caller
// (AIOCR limits anonymous chat by it). Ingress addresses are dynamic, so known networks are cleared.
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor };
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);
app.UseGlobalExceptionHandling();
app.UseCors(BrowserCorsExtensions.PolicyName);
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
