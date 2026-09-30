using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

namespace WardMate.Services.ProcedureCatalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProcedureInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProcedureDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("ProcedureDatabase")
                ?? throw new InvalidOperationException("Chưa cấu hình kết nối database của Procedure Catalog."))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IProcedureRepository, ProcedureRepository>();
        return services;
    }
}
