using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using WardMate.Services.ProcedureCatalog.Application.Management;

namespace WardMate.Services.ProcedureCatalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProcedureApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddScoped<IValidator<ProcedureInput>, ProcedureInputValidator>();
        services.AddScoped<IValidator<UpdateProcedureInput>, UpdateProcedureInputValidator>();
        return services;
    }
}
