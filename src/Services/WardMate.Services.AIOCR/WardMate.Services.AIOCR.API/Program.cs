using WardMate.SharedKernel.Web;
using WardMate.Services.AIOCR.API;
using WardMate.Services.AIOCR.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Short-lived cache of IAM access-context answers shared by every IAM HttpClient in this service.
builder.Services.AddAccessContextCache();

builder.Services.AddControllers();
var health = builder.Services.AddHealthChecks();
// Chat/Knowledge Base need the AIOCR database; without it only CCCD/verify run, so it is not checked.
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("AiOcrDatabase")))
    health.AddReadyProbe<ChatDbContext>("database", (db, ct) => db.Database.CanConnectAsync(ct));
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
    app.UseMiddleware<WardMate.SharedKernel.Web.AccountSessionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapWardMateHealth();
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.AIOCR" }));

app.Run();
