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
    private readonly ILogger<BlobStorageServiceRouter> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public BlobStorageServiceRouter(
        AzureBlobStorageService azureService,
        DefaultBlobStorageService defaultService,
        ILogger<BlobStorageServiceRouter> logger,
        ILoggerFactory loggerFactory)
    {
        _azureService = azureService;
        _defaultService = defaultService;
        _logger = logger;
        _loggerFactory = loggerFactory;
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
            {
                var awsLogger = _loggerFactory.CreateLogger<AwsS3BlobStorageService>();
                var awsService = new AwsS3BlobStorageService(awsLogger);
                return awsService.GetReadStreamAsync(blobUri);
            }
            case "GCS":
            {
                var gcsLogger = _loggerFactory.CreateLogger<GcsBlobStorageService>();
                var gcsService = new GcsBlobStorageService(gcsLogger);
                return gcsService.GetReadStreamAsync(blobUri);
            }
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
            {
                var awsLogger = _loggerFactory.CreateLogger<AwsS3BlobStorageService>();
                var awsService = new AwsS3BlobStorageService(awsLogger);
                return awsService.GetWriteStreamAsync(blobUri);
            }
            case "GCS":
            {
                var gcsLogger = _loggerFactory.CreateLogger<GcsBlobStorageService>();
                var gcsService = new GcsBlobStorageService(gcsLogger);
                return gcsService.GetWriteStreamAsync(blobUri);
            }
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
            {
                var awsLogger = _loggerFactory.CreateLogger<AwsS3BlobStorageService>();
                var awsService = new AwsS3BlobStorageService(awsLogger);
                return awsService.GetWriteStreamAsync(blobUri, appendMode);
            }
            case "GCS":
            {
                var gcsLogger = _loggerFactory.CreateLogger<GcsBlobStorageService>();
                var gcsService = new GcsBlobStorageService(gcsLogger);
                return gcsService.GetWriteStreamAsync(blobUri, appendMode);
            }
            default:
                _logger.LogWarning("Unknown or unsupported blob provider for URI: {BlobUri}. Using default.", blobUri);
                return _defaultService.GetWriteStreamAsync(blobUri, appendMode);
        }
    }
}
