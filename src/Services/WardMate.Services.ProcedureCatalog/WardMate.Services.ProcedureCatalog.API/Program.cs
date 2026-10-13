using WardMate.SharedKernel.Web;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.ProcedureCatalog.Application;
using WardMate.Services.ProcedureCatalog.Infrastructure;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;
using WardMate.Services.ProcedureCatalog.API.Security;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.DefaultIgnoreCondition =
    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddProcedureApplication();
builder.Services.AddProcedureInfrastructure(builder.Configuration);
builder.Services.AddProcedureAuthentication(builder.Configuration);
builder.Services.AddWardMateBrowserCors(builder.Configuration, builder.Environment);
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var errors = context.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key,
        x => new[] { "Giá trị không hợp lệ, thiếu trường bắt buộc hoặc không đúng định dạng JSON." });
    var problem = new ValidationProblemDetails(errors)
    {
        Status = 400, Title = "Dữ liệu không hợp lệ.", Instance = context.HttpContext.Request.Path,
        Extensions = { ["code"] = "validation.failed", ["traceId"] = context.HttpContext.TraceIdentifier }
    };
    var response = new BadRequestObjectResult(problem);
    response.ContentTypes.Add("application/problem+json");
    return response;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddGlobalExceptionHandling();

var app = builder.Build();

if (builder.Configuration.GetValue("Database:AutoMigrate", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ProcedureDbContext>().Database.MigrateAsync();
}

app.UseGlobalExceptionHandling();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors(BrowserCorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.ProcedureCatalog" }));

app.Run();

public partial class Program;
