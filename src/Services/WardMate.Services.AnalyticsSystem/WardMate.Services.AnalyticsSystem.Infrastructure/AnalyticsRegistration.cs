using WardMate.Auditing;
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.AnalyticsSystem.Application;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public static class AnalyticsRegistration
{
    public static IServiceCollection AddAnalytics(this IServiceCollection services, IConfiguration config)
    {
        // Same service owns notifications and analytics; separate schema/history avoids collisions.
        var connection = config.GetConnectionString("AnalyticsDatabase") ?? config.GetConnectionString("NotificationsDatabase")
            ?? throw new InvalidOperationException("Cần ConnectionStrings:AnalyticsDatabase hoặc NotificationsDatabase.");
        services.AddSourceAudit<AnalyticsDbContext>(config, "analytics");
        services.AddDbContext<AnalyticsDbContext>((sp, o) => o.AddInterceptors(sp.GetRequiredService<AuditConnectionInterceptor>()).UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__AnalyticsMigrationsHistory", "analytics")));
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ISystemHealth, SystemHealth>();
        services.AddScoped<ISystemBackups, SystemBackups>();
        services.AddHostedService<SystemHealthWorker>();
        services.AddHostedService<SystemBackupWorker>();

        var blobConn = config.GetSection(BlobStorageOptions.SectionName).GetValue<string>("ConnectionString");
        if (!string.IsNullOrWhiteSpace(blobConn))
        {
            services.AddAzureBlobStorage(config);
        }
        services.AddHttpClient("system-health", http =>
        {
            http.Timeout = TimeSpan.FromSeconds(5);
            http.MaxResponseContentBufferSize = 65536;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<ISystemAdministration, SystemAdministration>();
        services.AddScoped<IBusinessAuditStore, BusinessAuditStore>();
        services.AddScoped<AnalyticsAggregationJob>();
        services.AddHttpClient<IAnalyticsWorkflowClient, AnalyticsWorkflowClient>(http =>
        {
            http.BaseAddress = new Uri((config["Workflow:BaseUrl"] ?? "http://localhost:5003/").TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(60);
            http.MaxResponseContentBufferSize = 16 * 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        if (config.GetValue("Analytics:JobsEnabled", false))
        {
            if (Encoding.UTF8.GetByteCount(config["Analytics:ServiceKey"] ?? "") < 32)
                throw new InvalidOperationException("Analytics:ServiceKey cần ít nhất 32 byte, giống Workflow.");
            if (config.GetValue("Analytics:LookbackDays", 7) is < 1 or > 366)
                throw new InvalidOperationException("Analytics:LookbackDays phải từ 1 đến 366.");
            services.AddHangfire(h => h.UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connection), new PostgreSqlStorageOptions { SchemaName = "analytics_jobs" }));
            services.AddHangfireServer(o => o.WorkerCount = 1);
        }
        return services;
    }
}
