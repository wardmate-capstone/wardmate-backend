using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace WardMate.Services.AnalyticsSystem.API;

public static class NotificationSecurity
{
    public static IServiceCollection AddNotificationSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"] ?? "";
        if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Cần khóa Jwt:Key ít nhất 32 byte, giống IAM.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = true, ValidIssuer = configuration["Jwt:Issuer"] ?? "wardmate",
                ValidateAudience = true, ValidAudience = configuration["Jwt:Audience"] ?? "wardmate-client",
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub", RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Browsers cannot set Authorization on WebSocket/SSE; only accept query tokens on this hub.
                    if (context.Request.Path.StartsWithSegments("/hubs/notifications") &&
                        !context.Request.Headers.ContainsKey("Authorization") &&
                        context.Request.Query.TryGetValue("access_token", out var token) && token.Count == 1)
                        context.Token = token.ToString();
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) || id == Guid.Empty)
                        context.Fail("Mã người dùng không hợp lệ.");
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorization();
        return services;
    }
}
