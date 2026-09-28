using WardMate.SharedKernel.Blob;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class BlobStorageOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new BlobStorageOptions();

        Assert.Equal("AzureBlob", BlobStorageOptions.SectionName);
        Assert.Equal(string.Empty, options.ConnectionString);
        Assert.Equal("wardmate-files", options.ContainerName);
        Assert.Equal(20 * 1024 * 1024, options.MaxFileSizeBytes);
    }

    [Fact]
    public void CustomValues_AreRetained()
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "avatars",
            MaxFileSizeBytes = 5 * 1024 * 1024
        };

        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal("avatars", options.ContainerName);
        Assert.Equal(5 * 1024 * 1024, options.MaxFileSizeBytes);
    }
}
