using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WardMate.SharedKernel.Blob;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IBlobStorageClient"/>.
/// Automatically creates the target container with private access if it does not exist.
/// </summary>
internal sealed class AzureBlobStorageClient(
    BlobServiceClient serviceClient,
    IOptions<BlobStorageOptions> options,
    ILogger<AzureBlobStorageClient> logger) : IBlobStorageClient
{
    private readonly BlobStorageOptions _options = options.Value;

    // ── Upload ────────────────────────────────────────────────────────────────

    public async Task<string> UploadAsync(
        Stream stream,
        string blobName,
        string contentType,
        string? containerName = null,
        CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(containerName, ct);
        var client = container.GetBlobClient(blobName);

        await client.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        logger.LogInformation("Blob uploaded: {BlobName} to {Container}", blobName, container.Name);
        return client.Uri.ToString();
    }

    // ── Download ──────────────────────────────────────────────────────────────

    public async Task<Stream?> DownloadAsync(
        string blobName,
        string? containerName = null,
        CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(containerName, ct);
        var client = container.GetBlobClient(blobName);

        if (!await client.ExistsAsync(ct))
        {
            logger.LogWarning("Blob not found: {BlobName} in {Container}", blobName, container.Name);
            return null;
        }

        var response = await client.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(
        string blobName,
        string? containerName = null,
        CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(containerName, ct);
        var client = container.GetBlobClient(blobName);
        await client.DeleteIfExistsAsync(cancellationToken: ct);
        logger.LogInformation("Blob deleted (if existed): {BlobName}", blobName);
    }

    // ── Exists ────────────────────────────────────────────────────────────────

    public async Task<bool> ExistsAsync(
        string blobName,
        string? containerName = null,
        CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(containerName, ct);
        var client = container.GetBlobClient(blobName);
        var response = await client.ExistsAsync(ct);
        return response.Value;
    }

    // ── SAS URL ───────────────────────────────────────────────────────────────

    public Task<string> GenerateSasUrlAsync(
        string blobName,
        TimeSpan validFor,
        string? containerName = null,
        CancellationToken ct = default)
    {
        var container = serviceClient.GetBlobContainerClient(containerName ?? _options.ContainerName);
        var client = container.GetBlobClient(blobName);

        if (!client.CanGenerateSasUri)
            throw new InvalidOperationException(
                "BlobServiceClient was not initialised with a StorageSharedKeyCredential; SAS generation is unavailable.");

        var sasUri = client.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(validFor));
        return Task.FromResult(sasUri.ToString());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<BlobContainerClient> GetOrCreateContainerAsync(
        string? overrideName,
        CancellationToken ct)
    {
        var name = overrideName ?? _options.ContainerName;
        var container = serviceClient.GetBlobContainerClient(name);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        return container;
    }
}
