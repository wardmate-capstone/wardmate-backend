using System.Collections.Concurrent;
using WardMate.SharedKernel.Blob;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

internal sealed class DraftTestBlobStorage : IBlobStorageClient
{
    private readonly ConcurrentDictionary<string, byte[]> files = new();
    public async Task<string> UploadAsync(Stream stream, string blobName, string contentType, string? containerName = null, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream(); await stream.CopyToAsync(buffer, ct);
        files[blobName] = buffer.ToArray();
        return $"https://storage.example.test/{containerName}/{blobName}";
    }
    public Task<Stream?> DownloadAsync(string blobName, string? containerName = null, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(files.TryGetValue(blobName, out var bytes) ? new MemoryStream(bytes) : null);
    public Task DeleteAsync(string blobName, string? containerName = null, CancellationToken ct = default) { files.TryRemove(blobName, out _); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string blobName, string? containerName = null, CancellationToken ct = default) => Task.FromResult(files.ContainsKey(blobName));
    public Task<string> GenerateSasUrlAsync(string blobName, TimeSpan validFor, string? containerName = null, CancellationToken ct = default) =>
        Task.FromResult($"https://storage.example.test/{containerName}/{blobName}?test-only=true");
}
