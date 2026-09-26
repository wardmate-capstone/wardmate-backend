using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardMate.SharedKernel.Blob;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class BlobServiceExtensionsTests
{
    [Fact]
    public void AddAzureBlobStorage_RegistersServicesCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureBlob:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;",
                ["AzureBlob:ContainerName"] = "my-container",
                ["AzureBlob:MaxFileSizeBytes"] = "10485760"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAzureBlobStorage(configuration);

        var blobClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IBlobStorageClient));
        var azureClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(BlobServiceClient));

        Assert.NotNull(blobClientDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, blobClientDescriptor.Lifetime);

        Assert.NotNull(azureClientDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, azureClientDescriptor.Lifetime);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
        Assert.Equal("my-container", options.ContainerName);
        Assert.Equal(10485760, options.MaxFileSizeBytes);

        var client = provider.GetService<IBlobStorageClient>();
        Assert.NotNull(client);
    }
}
