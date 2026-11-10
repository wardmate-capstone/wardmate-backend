using WardMate.Auditing;
using WardMate.Services.DocumentForm.API.Security;

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

    builder.Services.AddDocumentAuthentication(builder.Configuration);

    builder.Services.AddHealthChecks().AddReadyProbe<DocumentDbContext>("database", (db, ct) => db.Database.CanConnectAsync(ct));

    builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);

    // DOC-QR-06a: API QR công khai giới hạn theo IP người gọi để không dò hàng loạt.
    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = 429;
        o.AddPolicy("public-qr", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            CallerIp.Of(context), _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Clamp(builder.Configuration.GetValue("PublicQr:MaxRequestsPerMinutePerIp", 30), 5, 1000),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
        // DOC-TPL-PUB-01: tải/xem phôi mẫu công khai; mỗi lần "Xem" trên FE cũng là một lần tải nên hạn mức rộng hơn QR.
        o.AddPolicy("public-forms", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            CallerIp.Of(context), _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Clamp(builder.Configuration.GetValue("PublicForms:MaxRequestsPerMinutePerIp", 60), 5, 1000),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
        o.OnRejected = async (context, ct) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            var forms = context.HttpContext.GetEndpoint()?.Metadata
                .GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName == "public-forms";
            var code = forms ? "document.rate_limited" : "qr.rate_limited";
            var problem = new ProblemDetails
            {
                Status = 429,
                Title = code,
                Detail = forms ? "Bạn tải mẫu đơn quá nhiều lần. Vui lòng đợi một phút rồi thử lại."
                    : "Bạn tra cứu mã QR quá nhiều lần. Vui lòng đợi một phút rồi thử lại.",
                Instance = context.HttpContext.Request.Path
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            await context.HttpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
        };
    });

    builder.Services.AddGlobalExceptionHandling();

    builder.Services.Configure<ProblemDetailsOptions>(options =>

        options.CustomizeProblemDetails = context =>

        {

            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);

        });



    // ── Application / Infrastructure ─────────────────────────────────────────

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHttpClient<ApplicationAccess>(client => client.Timeout = TimeSpan.FromSeconds(10))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
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

    app.UseAuditTrustedProxy(builder.Configuration);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<WardMate.SharedKernel.Web.AccountSessionMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    app.MapWardMateHealth();

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

