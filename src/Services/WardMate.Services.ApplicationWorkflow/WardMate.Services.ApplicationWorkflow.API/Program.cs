using WardMate.SharedKernel.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Infrastructure;
using WardMate.Services.ApplicationWorkflow.API;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState.Where(x => x.Value?.Errors.Count > 0)
        .ToDictionary(x => x.Key, _ => new[] { "Trường dữ liệu bị thiếu hoặc không đúng định dạng." }))
    { Status = 400, Title = "Dữ liệu không hợp lệ.", Extensions = { ["code"] = "validation.failed" } }));
var deadlinePolicy = builder.Configuration.GetSection("ReviewDeadline").Get<WardMate.Services.ApplicationWorkflow.Domain.ReviewDeadlinePolicy>() ?? new();
if (deadlinePolicy.Enabled) deadlinePolicy.Validate();
builder.Services.AddSingleton(deadlinePolicy);
builder.Services.AddWorkflowApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IDocumentContentsClient, DocumentContentsClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddWorkflowInfrastructure(builder.Configuration);
builder.Services.AddWorkflowSecurity(builder.Configuration);
builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddExceptionHandler<WorkflowValidationExceptionHandler>();
builder.Services.AddGlobalExceptionHandling();

var app = builder.Build();

if (builder.Configuration.GetValue("Database:AutoMigrate", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Database.MigrateAsync();
}

app.UseGlobalExceptionHandling();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled")) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors(BrowserCorsExtensions.PolicyName);
app.UseAuthentication();
app.UseMiddleware<WorkflowAccessMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.ApplicationWorkflow" }));

app.Run();

public partial class Program;


