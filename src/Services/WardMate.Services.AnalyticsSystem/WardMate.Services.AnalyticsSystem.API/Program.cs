using WardMate.SharedKernel.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandling();

var app = builder.Build();

app.UseGlobalExceptionHandling();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.AnalyticsSystem" }));

app.Run();
