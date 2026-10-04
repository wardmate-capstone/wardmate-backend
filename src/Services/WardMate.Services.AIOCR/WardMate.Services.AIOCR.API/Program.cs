using WardMate.SharedKernel.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddHttpClient<WardMate.Services.AIOCR.Application.Extraction.IProcedureDocumentExtractor,
    WardMate.Services.AIOCR.Infrastructure.Extraction.AzureProcedureDocumentExtractor>(http =>
    {
        http.Timeout = TimeSpan.FromMinutes(5);
        http.MaxResponseContentBufferSize = 16 * 1024 * 1024;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

var app = builder.Build();

app.UseGlobalExceptionHandling();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.AIOCR" }));

app.Run();
