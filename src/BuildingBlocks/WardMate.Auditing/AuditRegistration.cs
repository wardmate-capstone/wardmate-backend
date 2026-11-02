using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WardMate.Auditing;

public static class AuditRegistration
{
    public static IServiceCollection AddSourceAudit<T>(this IServiceCollection services, IConfiguration config, string source) where T:DbContext
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton<AuditConnectionInterceptor>();
        if (config.GetValue("Audit:RelayEnabled",false))
        {
            var key=config[$"Audit:SigningKeys:{source}"] ?? config["Audit:SigningKey"] ?? "";
            if (System.Text.Encoding.UTF8.GetByteCount(key)<32) throw new InvalidOperationException("Audit:SigningKey requires at least 32 bytes.");
            if (!Uri.TryCreate(config["Audit:CollectorUrl"],UriKind.Absolute,out var uri) ||
                (uri.Scheme!="https" && !(uri.Scheme=="http" && uri.IsLoopback)) ||
                uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)
                throw new InvalidOperationException("Audit:CollectorUrl requires HTTPS (HTTP only loopback for local checks).");
            services.AddHttpClient("audit-relay", h=>h.Timeout=TimeSpan.FromSeconds(10))
                .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler { AllowAutoRedirect=false });
            services.AddSingleton(new AuditRelayOptions<T>(source,uri,key));
            services.AddHostedService<AuditRelay<T>>();
        }
        return services;
    }
}
public sealed record AuditRelayOptions<T>(string Source, Uri Url, string Key);
