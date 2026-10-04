using Serilog;
using WardMate.SharedKernel.Logging;
using WardMate.SharedKernel.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application;
using WardMate.Services.IAM.Infrastructure;
using WardMate.Services.IAM.Infrastructure.Persistence;
using WardMate.Services.IAM.API.Security;

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
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    }).ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(
            context.ModelState.Where(entry => entry.Value?.Errors.Count > 0).ToDictionary(
                entry => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                entry => new[] { "Trường dữ liệu bị thiếu hoặc không đúng định dạng." }))
        {
            Status = 400,
            Title = "Dữ liệu không hợp lệ.",
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
            context.ProblemDetails.Title = context.HttpContext.Response.StatusCode switch
            {
                400 => "Dữ liệu không hợp lệ.",
                401 => "Vui lòng đăng nhập để tiếp tục.",
                403 => "Bạn không có quyền thực hiện thao tác này.",
                404 => "Không tìm thấy tài nguyên yêu cầu.",
                405 => "Phương thức yêu cầu không được hỗ trợ.",
                415 => "Định dạng nội dung không được hỗ trợ.",
                429 => "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.",
                >= 500 => "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
                _ => "Không thể xử lý yêu cầu."
            };
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
            context.ProblemDetails.Extensions.TryAdd("code", context.HttpContext.Response.StatusCode switch
            {
                401 => "iam.unauthorized",
                403 => "iam.forbidden",
                _ => "http_error"
            });
        });

    // ── Application / Infrastructure ─────────────────────────────────────────
    builder.Services.AddIamApplication();
    builder.Services.AddIamInfrastructure(builder.Configuration);
    builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);
    builder.Services.AddOptions<RefreshCookieOptions>()
        .Configure(options => options.AllowInsecureLocalhost = builder.Environment.IsDevelopment())
        .Bind(builder.Configuration.GetSection("AuthCookie"))
        .Validate(options => options.SameSite is SameSiteMode.Strict or SameSiteMode.Lax or SameSiteMode.None,
            "SameSite phải là Strict, Lax hoặc None.")
        .Validate(options => !options.AllowInsecureLocalhost || builder.Environment.IsDevelopment(),
            "Chỉ được cho phép cookie HTTP localhost trong Development.")
        .Validate(options => !options.AllowInsecureLocalhost || options.SameSite != SameSiteMode.None,
            "SameSite=None yêu cầu cookie Secure.")
        .ValidateOnStart();
    builder.Services.AddScoped<RefreshTokenCookie>();
    WardMate.Services.IAM.API.Authorization.PermissionAuthorization.AddPermissionAuthorization(builder.Services);

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
            Description = "Dán mã truy cập vào đây."
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
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
    };
    // Ingress addresses are dynamic in Azure/container networks. The ingress must sanitize these
    // headers and be the only network path to this service. Keep the default one-hop limit.
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseGlobalExceptionHandling();
    app.UseForwardedHeaders(forwardedHeadersOptions);
    app.UseWardMateRequestLogging();          // Serilog HTTP request logging
    app.UseWardMateRequestLoggingMiddleware(); // Custom detailed middleware
    app.UseStatusCodePages();
    app.UseCors(BrowserCorsExtensions.PolicyName);
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
