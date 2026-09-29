using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Services;

namespace WardMate.Services.DocumentForm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentFormApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddSingleton<IFormSchemaEngine, FormSchemaEngine>();

        return services;
    }
}
