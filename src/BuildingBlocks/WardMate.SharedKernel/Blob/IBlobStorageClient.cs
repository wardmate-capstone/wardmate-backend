namespace WardMate.SharedKernel.Blob;

/// <summary>
/// Contract for Azure Blob Storage operations used across all WardMate services.
/// Each service injects this interface; the concrete implementation is registered
/// in the SharedKernel DI extensions.
/// </summary>
public interface IBlobStorageClient
{
    /// <summary>
    /// Uploads a stream and returns the publicly accessible blob URL.
    /// </summary>
    /// <param name="stream">File content stream (must be readable and seekable).</param>
    /// <param name="blobName">Unique name for the blob, e.g. "documents/{guid}/phoi-mau.docx".</param>
    /// <param name="contentType">MIME type, e.g. "application/vnd.openxmlformats-officedocument.wordprocessingml.document".</param>
    /// <param name="containerName">Override container; null uses the default from options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Absolute URL to the uploaded blob.</returns>
    Task<string> UploadAsync(
        Stream stream,
        string blobName,
        string contentType,
        string? containerName = null,
        CancellationToken ct = default);

    /// <summary>Downloads a blob as a stream. Returns null when the blob does not exist.</summary>
    Task<Stream?> DownloadAsync(
        string blobName,
        string? containerName = null,
        CancellationToken ct = default);

    /// <summary>Deletes a blob. Does not throw if the blob does not exist.</summary>
    Task DeleteAsync(
        string blobName,
        string? containerName = null,
        CancellationToken ct = default);

    /// <summary>Checks whether a blob exists in the given (or default) container.</summary>
    Task<bool> ExistsAsync(
        string blobName,
        string? containerName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a time-limited Shared Access Signature URL for client-side download.
    /// </summary>
    Task<string> GenerateSasUrlAsync(
        string blobName,
        TimeSpan validFor,
        string? containerName = null,
        CancellationToken ct = default);
}
