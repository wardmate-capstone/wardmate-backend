using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WardMate.SharedKernel.Web;

public static class BrowserCorsExtensions
{
    public const string PolicyName = "WardMateBrowser";

    public static IServiceCollection AddWardMateBrowserCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? (environment.IsDevelopment() ? ["http://localhost:5173", "http://localhost:3000"] : Array.Empty<string>());
        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") || origin.Contains('*') || uri.GetLeftPart(UriPartial.Authority) != origin ||
                (!environment.IsDevelopment() && uri.Scheme != "https"))
                throw new InvalidOperationException("Cors:AllowedOrigins chỉ được chứa origin chính xác, không có wildcard/path; ngoài Development phải dùng HTTPS.");
        }
        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            if (origins.Length > 0) policy.WithOrigins(origins);
            policy.WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .WithHeaders("Content-Type", "Authorization", "X-CSRF-Protection").AllowCredentials();
        }));
        return services;
    }
}
