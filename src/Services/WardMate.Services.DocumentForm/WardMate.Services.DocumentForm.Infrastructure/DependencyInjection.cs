using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Infrastructure.OpenXml;
using WardMate.Services.DocumentForm.Infrastructure.Persistence;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.DocumentForm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentFormInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core — PostgreSQL + snake_case
        var connectionString = configuration.GetConnectionString("DocumentDb")
            ?? throw new InvalidOperationException("Connection string 'DocumentDb' is not configured.");

        services.AddDbContext<DocumentDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IDocumentDbContext>(sp => sp.GetRequiredService<DocumentDbContext>());

        // Azure Blob Storage (dùng SharedKernel wrapper)
        services.AddAzureBlobStorage(configuration);

        // OpenXML Placeholder Engine
        services.AddSingleton<IDocxPlaceholderEngine, DocxPlaceholderEngine>();

        return services;
    }
}
