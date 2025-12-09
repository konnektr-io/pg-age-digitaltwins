using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// AWS S3 implementation of blob storage service (stub).
/// </summary>
public class AwsS3BlobStorageService(ILogger<AwsS3BlobStorageService> logger) : IBlobStorageService
{
    private readonly ILogger<AwsS3BlobStorageService> _logger = logger;

    public Task<Stream> GetReadStreamAsync(Uri blobUri)
    {
        _logger.LogWarning("AWS S3 blob access not yet implemented: {BlobUri}", blobUri);
        throw new NotImplementedException("AWS S3 blob access not yet implemented.");
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        _logger.LogWarning("AWS S3 blob write not yet implemented: {BlobUri}", blobUri);
        throw new NotImplementedException("AWS S3 blob write not yet implemented.");
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode)
    {
        _logger.LogWarning("AWS S3 blob write (appendMode) not yet implemented: {BlobUri}", blobUri);
        throw new NotImplementedException("AWS S3 blob write (appendMode) not yet implemented.");
    }
}
