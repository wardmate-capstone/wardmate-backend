using WardMate.Auditing;
using WardMate.SharedKernel.Web;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.AnalyticsSystem.Infrastructure;
using WardMate.Services.AnalyticsSystem.API;
using Hangfire;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
// Short-lived cache of IAM access-context answers shared by every IAM HttpClient in this service.
builder.Services.AddAccessContextCache();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "WardMate Analytics & Citizen Feedback", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new() { [new OpenApiSecurityScheme { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
});
builder.Services.AddHealthChecks()
    .AddReadyProbe<NotificationDbContext>("notifications-database", (db, ct) => db.Database.CanConnectAsync(ct))
    .AddReadyProbe<AnalyticsDbContext>("analytics-database", (db, ct) => db.Database.CanConnectAsync(ct))
    .AddCheck<WardMate.EventBus.RabbitHealthCheck>("rabbitmq", tags: [DependencyHealth.ReadyTag]);
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

if (builder.Configuration.GetValue("SystemAdministration:Enabled", false) ||
    builder.Configuration.GetValue("SystemHealth:Enabled", false) ||
    builder.Configuration.GetValue("SystemBackups:Enabled", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply Analytics migrations before enabling SystemAdministration:Enabled.");
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
app.UseAuditTrustedProxy(builder.Configuration);
app.UseMiddleware<SystemAuditContextMiddleware>();
    app.UseAuthentication();
    app.UseMiddleware<WardMate.SharedKernel.Web.AccountSessionMiddleware>();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notifications", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
app.MapControllers();
app.MapWardMateHealth();
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.AnalyticsSystem" }));

app.Run();
