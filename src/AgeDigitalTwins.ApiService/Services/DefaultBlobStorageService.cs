namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Default implementation of blob storage service for testing and fallback.
/// Uses memory streams for testing.
/// </summary>
public class DefaultBlobStorageService(ILogger<DefaultBlobStorageService> logger)
    : IBlobStorageService
{
    private readonly ILogger<DefaultBlobStorageService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<Stream> GetReadStreamAsync(Uri blobUri)
    {
        _logger.LogWarning(
            "Blob URI access not yet implemented for URI scheme. Using empty stream: {BlobUri}",
            blobUri
        );

        // For testing purposes, return an empty memory stream
        // In a real implementation, this would parse the URI scheme and route to appropriate storage provider
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        // Default behavior is to overwrite for backward compatibility
        return GetWriteStreamAsync(blobUri, appendMode: false);
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode)
    {
        _logger.LogWarning(
            "Blob URI access not yet implemented for URI scheme. Using memory stream: {BlobUri} (append mode: {AppendMode})",
            blobUri,
            appendMode
        );

        // For testing purposes, return a memory stream
        // In a real implementation, this would parse the URI scheme and route to appropriate storage provider
        return Task.FromResult<Stream>(new MemoryStream());
    }
}
