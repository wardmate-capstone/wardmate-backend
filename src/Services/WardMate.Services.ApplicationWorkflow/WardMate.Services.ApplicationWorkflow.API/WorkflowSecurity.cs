using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace WardMate.Services.ApplicationWorkflow.API;

public sealed class WorkflowJwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "wardmate";
    public string Audience { get; set; } = "wardmate-client";
}
public static class WorkflowSecurity
{
    public static IServiceCollection AddWorkflowSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorkflowJwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(x => Encoding.UTF8.GetByteCount(x.Key ?? "") >= 32, "Cần khóa JWT ít nhất 32 byte, giống IAM.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience), "Issuer/Audience không được để trống.")
            .ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<Microsoft.Extensions.Options.IOptions<WorkflowJwtOptions>>((options, jwt) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key)),
                ValidateIssuer = true, ValidIssuer = jwt.Value.Issuer, ValidateAudience = true, ValidAudience = jwt.Value.Audience,
                ValidateLifetime = true, RequireSignedTokens = true, RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero, NameClaimType = "sub", RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) || id == Guid.Empty)
                        context.Fail("Mã người dùng không hợp lệ.");
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 401, Title = "Vui lòng đăng nhập bằng token hợp lệ.",
                        Extensions = { ["code"] = "auth.unauthorized" } }, options: null, contentType: "application/problem+json");
                }
            };
        });
        WardMate.SharedKernel.Web.FeaturePermissions.AddClaimPermissions(services, WardMate.SharedKernel.Web.FeaturePermissions.Workflow);
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "WardMate Application Workflow", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
            options.AddSecurityRequirement(new() { [new OpenApiSecurityScheme { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
        });
        return services;
    }
}
public sealed class WorkflowValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not ValidationException validation) return false;
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(validation.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()))
        { Status = 400, Title = "Dữ liệu không hợp lệ.", Instance = context.Request.Path,
            Extensions = { ["code"] = "validation.failed", ["traceId"] = context.TraceIdentifier } },
            options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}

