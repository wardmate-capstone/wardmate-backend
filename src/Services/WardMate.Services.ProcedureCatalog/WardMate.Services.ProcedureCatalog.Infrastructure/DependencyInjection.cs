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
        services.AddScoped<IProcedureManagementStore, ProcedureManagementStore>();
        services.AddScoped<WardMate.Services.ProcedureCatalog.Application.Management.IProcedureAdministration, ProcedureAdministration>();
        services.AddHttpClient<WardMate.Services.ProcedureCatalog.Application.Management.IDocumentFormsClient, DocumentFormsClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(15);
            http.MaxResponseContentBufferSize = 2 * 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        WardMate.SharedKernel.Blob.BlobServiceExtensions.AddAzureBlobStorage(services, configuration);
        services.AddScoped(provider => new Lazy<WardMate.SharedKernel.Blob.IBlobStorageClient>(
            provider.GetRequiredService<WardMate.SharedKernel.Blob.IBlobStorageClient>));
        services.AddSingleton(new WardMate.Services.ProcedureCatalog.Application.Drafts.DraftProcessingOptions(configuration.GetValue<bool>("ProcedureDrafts:ExtractionEnabled")));
        services.AddScoped<WardMate.Services.ProcedureCatalog.Application.Drafts.IDraftPersistence, Drafts.DraftPersistence>();
        services.AddScoped<WardMate.Services.ProcedureCatalog.Application.Drafts.IDraftFileStorage, Drafts.DraftFileStorage>();
        services.AddScoped<WardMate.Services.ProcedureCatalog.Application.Drafts.IProcedureDraftService, WardMate.Services.ProcedureCatalog.Application.Drafts.ProcedureDraftService>();
        services.AddHttpClient<WardMate.Services.ProcedureCatalog.Application.Drafts.IProcedureExtractor, Drafts.AiOcrClient>(http =>
        {
            http.Timeout = TimeSpan.FromMinutes(10);
            http.MaxResponseContentBufferSize = 4 * 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<Drafts.DraftExtractionWorker>();
        return services;
    }
}
