using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Services;

using Google.Cloud.Storage.V1;

/// <summary>
/// Google Cloud Storage implementation of blob storage service.
/// </summary>
public class GcsBlobStorageService : IBlobStorageService
{
    private readonly ILogger<GcsBlobStorageService> _logger;

    public GcsBlobStorageService(ILogger<GcsBlobStorageService> logger)
    {
        _logger = logger;
    }

    private static (string bucket, string objectName) ParseGcsUri(Uri uri)
    {
        // Support gs://bucket/object and https://storage.googleapis.com/bucket/object
        if (uri.Scheme == "gs")
        {
            var bucket = uri.Host;
            var objectName = uri.AbsolutePath.TrimStart('/');
            return (bucket, objectName);
        }
        if (uri.Host == "storage.googleapis.com")
        {
            var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
            if (segments.Length == 2)
                return (segments[0], segments[1]);
        }
        throw new ArgumentException($"Invalid GCS URI format: {uri}");
    }

    public async Task<Stream> GetReadStreamAsync(Uri blobUri)
    {
        var (bucket, objectName) = ParseGcsUri(blobUri);
        try
        {
            using var storageClient = StorageClient.Create(); // Uses default GCP credentials
            var ms = new MemoryStream();
            await storageClient.DownloadObjectAsync(bucket, objectName, ms);
            ms.Position = 0;
            _logger.LogInformation("Successfully opened read stream for GCS object: {Bucket}/{Object}", bucket, objectName);
            return ms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get read stream for GCS object: {Bucket}/{Object}", bucket, objectName);
            throw;
        }
    }

    public async Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        // GCS does not support direct streaming writes; use a MemoryStream and upload on dispose
        return await GetWriteStreamAsync(blobUri, appendMode: false);
    }

    public async Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode)
    {
        var (bucket, objectName) = ParseGcsUri(blobUri);
        byte[]? initialData = null;
        StorageClient storageClient = StorageClient.Create();
        if (appendMode)
        {
            try
            {
                var ms = new MemoryStream();
                await storageClient.DownloadObjectAsync(bucket, objectName, ms);
                initialData = ms.ToArray();
                _logger.LogInformation("Loaded existing GCS object for append: {Bucket}/{Object}, size={Size}", bucket, objectName, initialData.Length);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("No existing GCS object to append for: {Bucket}/{Object}", bucket, objectName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing GCS object for append: {Bucket}/{Object}. Will start fresh.", bucket, objectName);
            }
        }
        // Return a MemoryStream that uploads to GCS on dispose, initialized with previous data if appendMode
        var uploadStream = initialData != null
            ? new GcsUploadStream(bucket, objectName, _logger, initialData)
            : new GcsUploadStream(bucket, objectName, _logger);
        uploadStream.SetStorageClient(storageClient);
        return uploadStream;
    }

    /// <summary>
    /// MemoryStream that uploads to GCS on dispose.
    /// </summary>
    private class GcsUploadStream : MemoryStream
    {
        private StorageClient _storageClient;
        private readonly string _bucket;
        private readonly string _objectName;
        private readonly ILogger _logger;
        private bool _disposed;

        public GcsUploadStream(string bucket, string objectName, ILogger logger)
            : base()
        {
            _bucket = bucket;
            _objectName = objectName;
            _logger = logger;
        }

        public GcsUploadStream(string bucket, string objectName, ILogger logger, byte[] initialData)
            : base(initialData)
        {
            _bucket = bucket;
            _objectName = objectName;
            _logger = logger;
        }

        public void SetStorageClient(StorageClient storageClient)
        {
            _storageClient = storageClient;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    Position = 0;
                    _logger.LogDebug("Uploading stream to GCS: {Bucket}/{Object}", _bucket, _objectName);
                    _storageClient.UploadObject(_bucket, _objectName, null, this);
                    _logger.LogInformation("Successfully uploaded stream to GCS: {Bucket}/{Object}", _bucket, _objectName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload stream to GCS: {Bucket}/{Object}", _bucket, _objectName);
                    throw;
                }
                finally
                {
                    _disposed = true;
                    _storageClient?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
