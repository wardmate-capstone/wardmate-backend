using WardMate.SharedKernel.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseGlobalExceptionHandling();
app.UseCors(BrowserCorsExtensions.PolicyName);
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
