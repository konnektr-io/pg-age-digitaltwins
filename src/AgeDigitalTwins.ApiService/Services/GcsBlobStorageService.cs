using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Google Cloud Storage implementation of blob storage service (stub).
/// </summary>
public class GcsBlobStorageService(ILogger<GcsBlobStorageService> logger) : IBlobStorageService
{
    private readonly ILogger<GcsBlobStorageService> _logger = logger;

    public Task<Stream> GetReadStreamAsync(Uri blobUri)
    {
        _logger.LogWarning("Google Cloud Storage blob access not yet implemented: {BlobUri}", blobUri);
        throw new NotImplementedException("Google Cloud Storage blob access not yet implemented.");
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        _logger.LogWarning("Google Cloud Storage blob write not yet implemented: {BlobUri}", blobUri);
        throw new NotImplementedException("Google Cloud Storage blob write not yet implemented.");
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode)
    {
        _logger.LogWarning("Google Cloud Storage blob write (appendMode) not yet implemented: {BlobUri}", blobUri);
        throw new NotImplementedException("Google Cloud Storage blob write (appendMode) not yet implemented.");
    }
}
