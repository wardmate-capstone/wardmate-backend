using Microsoft.Extensions.DependencyInjection;

namespace WardMate.Services.ProcedureCatalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProcedureApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
