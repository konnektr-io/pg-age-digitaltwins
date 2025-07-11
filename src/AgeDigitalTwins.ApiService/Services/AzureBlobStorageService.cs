using Azure.Identity;
using Azure.Storage.Blobs;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Azure Blob Storage implementation of blob storage service.
/// Supports Azure Storage with managed identity authentication.
/// </summary>
public class AzureBlobStorageService(ILogger<AzureBlobStorageService> logger) : IBlobStorageService
{
    private readonly ILogger<AzureBlobStorageService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Stream> GetReadStreamAsync(Uri blobUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri.AbsoluteUri);

        try
        {
            _logger.LogDebug("Getting read stream for blob URI: {BlobUri}", blobUri);

            var blobClient = CreateBlobClient(blobUri);

            // Check if blob exists
            var response = await blobClient.ExistsAsync();
            if (!response.Value)
            {
                throw new FileNotFoundException($"Blob not found: {blobUri}");
            }

            // Get blob content as stream
            var downloadResponse = await blobClient.DownloadStreamingAsync();

            _logger.LogInformation("Successfully opened read stream for blob: {BlobUri}", blobUri);
            return downloadResponse.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get read stream for blob URI: {BlobUri}", blobUri);
            throw;
        }
    }

    public Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri.AbsoluteUri);

        try
        {
            _logger.LogDebug("Getting write stream for blob URI: {BlobUri}", blobUri);

            var blobClient = CreateBlobClient(blobUri);

            // Create a memory stream that will upload to blob when disposed
            var memoryStream = new BlobUploadStream(blobClient, _logger);

            _logger.LogInformation(
                "Successfully created write stream for blob: {BlobUri}",
                blobUri
            );
            return Task.FromResult<Stream>(memoryStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get write stream for blob URI: {BlobUri}", blobUri);
            throw;
        }
    }

    private static BlobClient CreateBlobClient(Uri blobUri)
    {
        // Use managed identity for authentication (preferred for Azure-hosted applications)
        // Falls back to other credential types as appropriate
        var credential = new DefaultAzureCredential();

        return new BlobClient(blobUri, credential);
    }

    /// <summary>
    /// Custom stream that uploads to blob storage when disposed.
    /// This allows streaming writes to blob storage.
    /// </summary>
    private class BlobUploadStream : MemoryStream
    {
        private readonly BlobClient _blobClient;
        private readonly ILogger _logger;
        private bool _disposed;

        public BlobUploadStream(BlobClient blobClient, ILogger logger)
        {
            _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Upload the memory stream content to blob storage
                    Position = 0;
                    _blobClient.Upload(this, overwrite: true);
                    _logger.LogInformation(
                        "Successfully uploaded content to blob: {BlobUri}",
                        _blobClient.Uri
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to upload content to blob: {BlobUri}",
                        _blobClient.Uri
                    );
                    throw;
                }
                finally
                {
                    _disposed = true;
                }
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    // Upload the memory stream content to blob storage
                    Position = 0;
                    await _blobClient.UploadAsync(this, overwrite: true);
                    _logger.LogInformation(
                        "Successfully uploaded content to blob: {BlobUri}",
                        _blobClient.Uri
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to upload content to blob: {BlobUri}",
                        _blobClient.Uri
                    );
                    throw;
                }
                finally
                {
                    _disposed = true;
                }
            }

            await base.DisposeAsync();
        }
    }
}
