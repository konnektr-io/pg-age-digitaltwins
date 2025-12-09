using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Routes blob storage operations to the correct provider implementation based on the URI.
/// </summary>
public class BlobStorageServiceRouter : IBlobStorageService
{
    private readonly AzureBlobStorageService _azureService;
    private readonly DefaultBlobStorageService _defaultService;
    private readonly AwsS3BlobStorageService _awsService;
    private readonly GcsBlobStorageService _gcsService;
    private readonly ILogger<BlobStorageServiceRouter> _logger;

    public BlobStorageServiceRouter(
        AzureBlobStorageService azureService,
        DefaultBlobStorageService defaultService,
        AwsS3BlobStorageService awsService,
        GcsBlobStorageService gcsService,
        ILogger<BlobStorageServiceRouter> logger)
    {
        _azureService = azureService;
        _defaultService = defaultService;
        _awsService = awsService;
        _gcsService = gcsService;
        _logger = logger;
    }

    private static string DetectProvider(Uri blobUri)
    {
        var host = blobUri.Host.ToLowerInvariant();
        var scheme = blobUri.Scheme.ToLowerInvariant();
        if (host.Contains("blob.core.windows.net")) return "Azure";
        if (host.Contains("s3.amazonaws.com") || scheme == "s3") return "S3";
        if (host.Contains("storage.googleapis.com") || scheme == "gs") return "GCS";
        return "Default";
    }

    public Task<Stream> GetReadStreamAsync(Uri blobUri)
    {
        switch (DetectProvider(blobUri))
        {
            case "Azure":
                return _azureService.GetReadStreamAsync(blobUri);
            case "S3":
                return _awsService.GetReadStreamAsync(blobUri);
            case "GCS":
                return _gcsService.GetReadStreamAsync(blobUri);
            default:
                _logger.LogWarning("Unknown or unsupported blob provider for URI: {BlobUri}. Using default.", blobUri);
                return _defaultService.GetReadStreamAsync(blobUri);
        }
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        switch (DetectProvider(blobUri))
        {
            case "Azure":
                return _azureService.GetWriteStreamAsync(blobUri);
            case "S3":
                return _awsService.GetWriteStreamAsync(blobUri);
            case "GCS":
                return _gcsService.GetWriteStreamAsync(blobUri);
            default:
                _logger.LogWarning("Unknown or unsupported blob provider for URI: {BlobUri}. Using default.", blobUri);
                return _defaultService.GetWriteStreamAsync(blobUri);
        }
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode)
    {
        switch (DetectProvider(blobUri))
        {
            case "Azure":
                return _azureService.GetWriteStreamAsync(blobUri, appendMode);
            case "S3":
                return _awsService.GetWriteStreamAsync(blobUri, appendMode);
            case "GCS":
                return _gcsService.GetWriteStreamAsync(blobUri, appendMode);
            default:
                _logger.LogWarning("Unknown or unsupported blob provider for URI: {BlobUri}. Using default.", blobUri);
                return _defaultService.GetWriteStreamAsync(blobUri, appendMode);
        }
    }
}
