using Serilog;
using WardMate.SharedKernel.Logging;
using WardMate.SharedKernel.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application;
using WardMate.Services.IAM.Infrastructure;
using WardMate.Services.IAM.Infrastructure.Persistence;

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
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
    builder.Services.AddGlobalExceptionHandling();
    builder.Services.Configure<Microsoft.AspNetCore.Http.ProblemDetailsOptions>(options =>
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
            context.ProblemDetails.Extensions.TryAdd("code",
                context.HttpContext.Response.StatusCode == 401 ? "iam.unauthorized" : "http_error");
        });

    // ── Application / Infrastructure ─────────────────────────────────────────
    builder.Services.AddIamApplication();
    builder.Services.AddIamInfrastructure(builder.Configuration);

    // ── Swagger ──────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "WardMate IAM API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the access token."
        });
        options.OperationFilter<WardMate.Services.IAM.API.OpenApi.BearerSecurityOperationFilter>();
    });

    var app = builder.Build();

    // ── EF migrations ────────────────────────────────────────────────────────
    if (builder.Configuration.GetValue("Database:AutoMigrate", true))
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IamDbContext>().Database.MigrateAsync();
    }

    // ── Middleware pipeline ──────────────────────────────────────────────────
    app.UseWardMateRequestLogging();          // Serilog HTTP request logging
    app.UseWardMateRequestLoggingMiddleware(); // Custom detailed middleware
    app.UseGlobalExceptionHandling();
    app.UseStatusCodePages();
    if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.IAM" }));

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "WardMate.IAM host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
