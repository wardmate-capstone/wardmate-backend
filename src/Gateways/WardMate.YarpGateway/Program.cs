using WardMate.SharedKernel.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseGlobalExceptionHandling();
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
