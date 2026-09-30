using WardMate.SharedKernel.Web;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.ProcedureCatalog.Application;
using WardMate.Services.ProcedureCatalog.Infrastructure;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.DefaultIgnoreCondition =
    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddProcedureApplication();
builder.Services.AddProcedureInfrastructure(builder.Configuration);
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
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "WardMate.Services.ProcedureCatalog" }));

app.Run();

public partial class Program;
