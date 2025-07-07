using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Default implementation of blob storage service for testing and fallback.
/// Uses memory streams for testing.
/// </summary>
public class DefaultBlobStorageService : IBlobStorageService
{
    private readonly ILogger<DefaultBlobStorageService> _logger;

    public DefaultBlobStorageService(ILogger<DefaultBlobStorageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Stream> GetReadStreamAsync(string blobUri)
    {
        _logger.LogWarning(
            "Blob URI access not yet implemented for URI scheme. Using empty stream: {BlobUri}",
            blobUri
        );

        // For testing purposes, return an empty memory stream
        // In a real implementation, this would parse the URI scheme and route to appropriate storage provider
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task<Stream> GetWriteStreamAsync(string blobUri)
    {
        _logger.LogWarning(
            "Blob URI access not yet implemented for URI scheme. Using memory stream: {BlobUri}",
            blobUri
        );

        // For testing purposes, return a memory stream
        // In a real implementation, this would parse the URI scheme and route to appropriate storage provider
        return Task.FromResult<Stream>(new MemoryStream());
    }
}
