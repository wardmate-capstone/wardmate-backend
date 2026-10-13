using WardMate.SharedKernel.Web;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.AnalyticsSystem.Infrastructure;
using WardMate.Services.AnalyticsSystem.API;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddNotifications(builder.Configuration);
builder.Services.AddNotificationSecurity(builder.Configuration);
builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);

var app = builder.Build();

if (builder.Configuration.GetValue("Database:AutoMigrate", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
}

app.UseGlobalExceptionHandling();
app.UseCors(BrowserCorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notifications", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.AnalyticsSystem" }));

app.Run();
