using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Infrastructure.Persistence;
using WardMate.Services.IAM.Infrastructure.Security;

namespace WardMate.Services.IAM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIamInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<IamDbContext>(options => options.UseNpgsql(
            config.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required."))
            .UseSnakeCaseNamingConvention());
        services.AddOptions<JwtOptions>().Bind(config.GetSection("Jwt"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key) && Encoding.UTF8.GetByteCount(o.Key) >= 32, "Jwt:Key must contain at least 32 UTF-8 bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience), "JWT issuer and audience are required.")
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 60 && o.RefreshTokenDays is >= 1 and <= 30, "Invalid token lifetime.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<IProfileStore, ProfileStore>();
        services.AddScoped<WardMate.Services.IAM.Application.Accounts.IAccountStore, AccountStore>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = jwt.Value.ValidationParameters();
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId))
                        {
                            context.Fail("Mã người dùng không hợp lệ.");
                            return;
                        }
                        var store = context.HttpContext.RequestServices.GetRequiredService<IIdentityStore>();
                        var user = await store.FindUser(userId, context.HttpContext.RequestAborted);
                        if (user is null || !user.IsActive) context.Fail("Tài khoản không khả dụng.");
                    }
                };
            });
        services.AddAuthorization();
        return services;
    }
}
