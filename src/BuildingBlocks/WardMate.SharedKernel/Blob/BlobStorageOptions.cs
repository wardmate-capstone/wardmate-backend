namespace WardMate.SharedKernel.Blob;

/// <summary>
/// Configuration options for Azure Blob Storage, bound from appsettings:
/// <code>
/// "AzureBlob": {
///   "ConnectionString": "DefaultEndpointsProtocol=https;...",
///   "ContainerName": "wardmate-files",
///   "MaxFileSizeBytes": 20971520
/// }
/// </code>
/// </summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "AzureBlob";

    /// <summary>Azure Storage account connection string or SAS URL.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Default blob container name. Container is created if it does not exist.</summary>
    public string ContainerName { get; init; } = "wardmate-files";

    /// <summary>Maximum allowed file size in bytes. Default 20 MB.</summary>
    public long MaxFileSizeBytes { get; init; } = 20 * 1024 * 1024;
}
