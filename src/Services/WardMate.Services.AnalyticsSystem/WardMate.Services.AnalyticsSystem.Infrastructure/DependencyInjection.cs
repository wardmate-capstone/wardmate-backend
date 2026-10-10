using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.AnalyticsSystem.Application;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("NotificationsDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Cần cấu hình ConnectionStrings:NotificationsDatabase.");
        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(connection));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<INotificationService, NotificationService>();
        services.AddHttpClient<INotificationDirectory, NotificationDirectory>(http =>
        {
            http.BaseAddress = new Uri(configuration["Iam:BaseUrl"] ?? "http://localhost:5001/");
            http.Timeout = TimeSpan.FromSeconds(10);
            http.MaxResponseContentBufferSize = 2 * 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.MaximumReceiveMessageSize = 16 * 1024;
        });
        return services;
    }
}
