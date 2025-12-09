using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// AWS S3 implementation of blob storage service.
/// </summary>
public class AwsS3BlobStorageService : IBlobStorageService
{
    private readonly ILogger<AwsS3BlobStorageService> _logger;

    public AwsS3BlobStorageService(ILogger<AwsS3BlobStorageService> logger)
    {
        _logger = logger;
    }

    private static (string bucket, string key) ParseS3Uri(Uri uri)
    {
        // Support s3://bucket/key and https://bucket.s3.amazonaws.com/key
        if (uri.Scheme == "s3")
        {
            var bucket = uri.Host;
            var key = uri.AbsolutePath.TrimStart('/');
            return (bucket, key);
        }
        if (uri.Host.EndsWith(".s3.amazonaws.com"))
        {
            var bucket = uri.Host.Substring(0, uri.Host.IndexOf(".s3.amazonaws.com"));
            var key = uri.AbsolutePath.TrimStart('/');
            return (bucket, key);
        }
        throw new ArgumentException($"Invalid S3 URI format: {uri}");
    }

    public async Task<Stream> GetReadStreamAsync(Uri blobUri)
    {
        var (bucket, key) = ParseS3Uri(blobUri);
        try
        {
            using var s3Client = new AmazonS3Client(); // Uses default credentials chain
            _logger.LogDebug("Getting S3 object: bucket={Bucket}, key={Key}", bucket, key);
            var response = await s3Client.GetObjectAsync(bucket, key);
            _logger.LogInformation("Successfully opened read stream for S3 object: {Bucket}/{Key}", bucket, key);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get read stream for S3 object: {Bucket}/{Key}", bucket, key);
            throw;
        }
    }

    public async Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        // S3 does not support direct streaming writes; use a MemoryStream and upload on dispose
        return await GetWriteStreamAsync(blobUri, appendMode: false);
    }

    public async Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode)
    {
        var (bucket, key) = ParseS3Uri(blobUri);
        byte[]? initialData = null;
        IAmazonS3 s3Client = new AmazonS3Client();
        if (appendMode)
        {
            try
            {
                // Try to download existing object
                var response = await s3Client.GetObjectAsync(bucket, key);
                using (var ms = new MemoryStream())
                {
                    await response.ResponseStream.CopyToAsync(ms);
                    initialData = ms.ToArray();
                    _logger.LogInformation("Loaded existing S3 object for append: {Bucket}/{Key}, size={Size}", bucket, key, initialData.Length);
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("No existing S3 object to append for: {Bucket}/{Key}", bucket, key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing S3 object for append: {Bucket}/{Key}. Will start fresh.", bucket, key);
            }
        }
        // Return a MemoryStream that uploads to S3 on dispose, initialized with previous data if appendMode
        var uploadStream = initialData != null
            ? new S3UploadStream(bucket, key, _logger, initialData)
            : new S3UploadStream(bucket, key, _logger);
        uploadStream.SetS3Client(s3Client);
        return uploadStream;
    }

    /// <summary>
    /// MemoryStream that uploads to S3 on dispose.
    /// </summary>
    private class S3UploadStream : MemoryStream
    {
        private IAmazonS3 _s3Client;
        private readonly string _bucket;
        private readonly string _key;
        private readonly ILogger _logger;
        private bool _disposed;

        public S3UploadStream(string bucket, string key, ILogger logger)
            : base()
        {
            _bucket = bucket;
            _key = key;
            _logger = logger;
        }

        public S3UploadStream(string bucket, string key, ILogger logger, byte[] initialData)
            : base(initialData)
        {
            _bucket = bucket;
            _key = key;
            _logger = logger;
        }

        public void SetS3Client(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    Position = 0;
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucket,
                        Key = _key,
                        InputStream = this,
                    };
                    _logger.LogDebug("Uploading stream to S3: {Bucket}/{Key}", _bucket, _key);
                    _s3Client.PutObjectAsync(putRequest).GetAwaiter().GetResult();
                    _logger.LogInformation("Successfully uploaded stream to S3: {Bucket}/{Key}", _bucket, _key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload stream to S3: {Bucket}/{Key}", _bucket, _key);
                    throw;
                }
                finally
                {
                    _disposed = true;
                    _s3Client?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
