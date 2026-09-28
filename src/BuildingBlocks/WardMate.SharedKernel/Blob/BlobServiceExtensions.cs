using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WardMate.SharedKernel.Blob;

/// <summary>
/// IServiceCollection extensions for registering Azure Blob Storage.
/// </summary>
public static class BlobServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IBlobStorageClient"/> (backed by Azure Blob Storage) into the DI container.
    /// Reads connection string and container name from the "AzureBlob" configuration section.
    /// </summary>
    /// <example>
    /// appsettings.json:
    /// <code>
    /// "AzureBlob": {
    ///   "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    ///   "ContainerName": "wardmate-files",
    ///   "MaxFileSizeBytes": 20971520
    /// }
    /// </code>
    /// Program.cs:
    /// <code>
    /// builder.Services.AddAzureBlobStorage(builder.Configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BlobStorageOptions>(
            configuration.GetSection(BlobStorageOptions.SectionName));

        var connectionString = configuration
            .GetSection(BlobStorageOptions.SectionName)
            .GetValue<string>("ConnectionString");

        // Register the Azure SDK BlobServiceClient as a singleton (thread-safe, one per app).
        services.AddSingleton(_ => new BlobServiceClient(connectionString));

        // Register our wrapper as scoped — it depends on IOptions which is singleton-safe.
        services.AddScoped<IBlobStorageClient, AzureBlobStorageClient>();

        return services;
    }
}
