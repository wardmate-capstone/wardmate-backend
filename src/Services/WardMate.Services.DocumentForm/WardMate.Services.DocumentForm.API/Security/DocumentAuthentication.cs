using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace WardMate.Services.DocumentForm.API.Security;

public static class DocumentAuthentication
{
    public static IServiceCollection AddDocumentAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DocumentJwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(x => Encoding.UTF8.GetByteCount(x.Key ?? string.Empty) >= 32, "Khóa JWT phải có ít nhất 32 byte; cấu hình Jwt__Key giống IAM.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience), "Issuer và Audience JWT không được để trống.")
            .ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<DocumentJwtOptions>>((options, jwt) =>
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
                    OnForbidden = context => WriteProblem(context.HttpContext, 403, "auth.forbidden", "Bạn không có quyền thực hiện thao tác này.")
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("DocumentCitizen", policy => policy.RequireAuthenticatedUser()
                .RequireRole("REGISTERED_CITIZEN")
                .RequireAssertion(context => Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) && id != Guid.Empty));
            options.AddPolicy("DocumentManager", policy => policy.RequireAuthenticatedUser().RequireRole("PROCEDURE_MANAGER"));
            options.AddPolicy("DocumentUser", policy => policy.RequireAuthenticatedUser()
                .RequireRole("REGISTERED_CITIZEN", "FRONT_DESK_OFFICER", "MANAGER", "PROCEDURE_MANAGER", "IT_ADMIN"));
        });
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Nhập Access Token từ IAM. Hỗ trợ các role: REGISTERED_CITIZEN, PROCEDURE_MANAGER, FRONT_DESK_OFFICER, MANAGER, IT_ADMIN."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
            });
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

public sealed class DocumentJwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "wardmate";
    public string Audience { get; set; } = "wardmate-client";
}
