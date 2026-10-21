using WardMate.SharedKernel.Web;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.AnalyticsSystem.Infrastructure;
using WardMate.Services.AnalyticsSystem.API;
using Hangfire;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "WardMate Analytics & Citizen Feedback", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new() { [new OpenApiSecurityScheme { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
});
builder.Services.AddHealthChecks();
builder.Services.AddExceptionHandler<AnalyticsExceptionHandler>();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddNotifications(builder.Configuration);
builder.Services.AddAnalytics(builder.Configuration);
builder.Services.AddNotificationSecurity(builder.Configuration);
builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);

var app = builder.Build();

if (builder.Configuration.GetValue("Database:AutoMigrate", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.MigrateAsync();
}

app.UseGlobalExceptionHandling();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{ app.UseSwagger(); app.UseSwaggerUI(); }
if (builder.Configuration.GetValue("Analytics:JobsEnabled", false))
{
    var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
    jobs.AddOrUpdate<AnalyticsAggregationJob>("analytics-daily", job => job.Refresh(CancellationToken.None),
        builder.Configuration["Analytics:Cron"] ?? "15 1 * * *", new RecurringJobOptions
        { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh") });
}
app.UseCors(BrowserCorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notifications", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.AnalyticsSystem" }));

app.Run();
