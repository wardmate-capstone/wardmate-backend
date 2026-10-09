using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using WardMate.Services.DocumentForm.Application;
using WardMate.Services.DocumentForm.Infrastructure;
using WardMate.Services.DocumentForm.Infrastructure.Persistence;
using WardMate.SharedKernel.Logging;
using WardMate.SharedKernel.Web;

// ── Bootstrap Serilog immediately so startup errors are captured ────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Centralised Serilog (Console + File, JSON in Prod) ───────────────────
    builder.AddWardMateLogging();

    // ── MVC / API ────────────────────────────────────────────────────────────
    builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
        {
            Status = 400,
            Title = "Validation failed.",
            Instance = context.HttpContext.Request.Path,
            Extensions = { ["code"] = "validation_failed", ["traceId"] = context.HttpContext.TraceIdentifier }
        });
    });

    // ── Health / Exception handling ──────────────────────────────────────────
    builder.Services.AddHealthChecks();
    builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);
    builder.Services.AddGlobalExceptionHandling();
    builder.Services.Configure<ProblemDetailsOptions>(options =>
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
        });

    // ── Application / Infrastructure ─────────────────────────────────────────
    builder.Services.AddDocumentFormApplication();
    builder.Services.AddDocumentFormInfrastructure(builder.Configuration);

    // ── Swagger ──────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "WardMate Document & Form Service API",
            Version = "v1",
            Description = "Original DOCX templates and citizen DOCX editor drafts. Download binary, edit in FE, upload edited DOCX, then finalize."
        });
    });

    var app = builder.Build();

    // ── EF migrations ────────────────────────────────────────────────────────
    if (builder.Configuration.GetValue("Database:AutoMigrate", true))
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply database migrations automatically on startup.");
        }
    }

    // ── Middleware pipeline ──────────────────────────────────────────────────
    app.UseWardMateRequestLogging();
    app.UseWardMateRequestLoggingMiddleware();
    app.UseGlobalExceptionHandling();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger(options => options.PreSerializeFilters.Add((document, _) =>
        {
            // Gateway exposes the /api/v1 routes at its origin root.
            document.Servers = new List<OpenApiServer> { new() { Url = "/" } };
        }));
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("./v1/swagger.json", "WardMate Document & Form API v1");
        });
    }

    app.UseCors(BrowserCorsExtensions.PolicyName);
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Ok(new
    {
        service = "WardMate.Services.DocumentForm",
        version = "v1",
        status = "Healthy"
    }));

    Log.Information("WardMate.Services.DocumentForm started successfully.");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "WardMate.Services.DocumentForm terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
