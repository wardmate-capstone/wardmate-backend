using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace WardMate.Services.ProcedureCatalog.API.Security;

public static class ProcedureAuthentication
{
    public static IServiceCollection AddProcedureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProcedureJwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(x => Encoding.UTF8.GetByteCount(x.Key ?? string.Empty) >= 32, "Khóa JWT phải có ít nhất 32 byte; cấu hình Jwt__Key giống IAM.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience), "Issuer và Audience JWT không được để trống.")
            .ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<ProcedureJwtOptions>>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key)),
                    ValidateIssuer = true, ValidIssuer = jwt.Value.Issuer, ValidateAudience = true, ValidAudience = jwt.Value.Audience,
                    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub", RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await WriteProblem(context.HttpContext, 401, "auth.unauthorized", "Bạn cần đăng nhập bằng token hợp lệ.");
                    },
                    OnForbidden = context => WriteProblem(context.HttpContext, 403, "auth.forbidden", "Bạn không có quyền quản lý thủ tục.")
                };
            });
        services.AddAuthorization(options => options.AddPolicy("ProcedureManager", policy =>
            policy.RequireAuthenticatedUser().RequireRole("PROCEDURE_MANAGER", "IT_ADMIN")));
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Nhập Access Token từ IAM. API quản lý yêu cầu vai trò PROCEDURE_MANAGER hoặc IT_ADMIN."
            });
            options.OperationFilter<ManagerSecurityOperationFilter>();
        });
        return services;
    }

    private static Task WriteProblem(HttpContext context, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = title, Instance = context.Request.Path,
            Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier }
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}

public sealed class ProcedureJwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "wardmate";
    public string Audience { get; set; } = "wardmate-client";
}
