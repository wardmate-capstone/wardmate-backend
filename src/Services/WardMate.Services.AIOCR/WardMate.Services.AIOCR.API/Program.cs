using WardMate.SharedKernel.Web;
using WardMate.Services.AIOCR.API;
using WardMate.Services.AIOCR.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.AddAssistant();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddHttpClient<WardMate.Services.AIOCR.Infrastructure.Extraction.AzureProcedureDocumentExtractor>(http =>
    {
        http.Timeout = TimeSpan.FromMinutes(5);
        http.MaxResponseContentBufferSize = 16 * 1024 * 1024;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<WardMate.Services.AIOCR.Application.Extraction.IProcedureDocumentExtractor,
    WardMate.Services.AIOCR.Infrastructure.Extraction.TextFirstProcedureExtractor>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("AiOcrDatabase")))
        throw new InvalidOperationException("AutoMigrate requires ConnectionStrings:AiOcrDatabase.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ChatDbContext>().Database.MigrateAsync();
}

app.UseGlobalExceptionHandling();
app.UseCors(BrowserCorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.AIOCR" }));

app.Run();
