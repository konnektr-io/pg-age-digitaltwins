using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

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

            // Use OpenReadAsync to get a stream that directly reads from blob storage
            var readStream = await blobClient.OpenReadAsync();

            _logger.LogInformation("Successfully opened read stream for blob: {BlobUri}", blobUri);
            return readStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get read stream for blob URI: {BlobUri}", blobUri);
            throw;
        }
    }

    public async Task<Stream> GetWriteStreamAsync(Uri blobUri)
    {
        // Default behavior is to overwrite for backward compatibility
        return await GetWriteStreamAsync(blobUri, appendMode: false);
    }

    public async Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri.AbsoluteUri);

        try
        {
            _logger.LogDebug(
                "Getting write stream for blob URI: {BlobUri} (append mode: {AppendMode})",
                blobUri,
                appendMode
            );

            // Always use AppendBlobClient for consistency - blob type cannot be changed once created
            var appendBlobClient = CreateAppendBlobClient(blobUri);

            // Check if blob exists
            var exists = await appendBlobClient.ExistsAsync();
            if (exists.Value)
            {
                // Check if it's already an append blob
                var properties = await appendBlobClient.GetPropertiesAsync();
                if (properties.Value.BlobType != Azure.Storage.Blobs.Models.BlobType.Append)
                {
                    _logger.LogWarning(
                        "Blob {BlobUri} exists but is not an AppendBlob (type: {BlobType}). "
                            + "Cannot use existing blob of different type.",
                        blobUri,
                        properties.Value.BlobType
                    );
                    throw new InvalidOperationException(
                        $"Cannot use existing blob of type {properties.Value.BlobType}. "
                            + "Blob must be an AppendBlob for this service."
                    );
                }

                if (appendMode)
                {
                    _logger.LogDebug("Existing AppendBlob found, will append to it");
                }
                else
                {
                    _logger.LogDebug("Existing AppendBlob found, will overwrite it");
                }
            }
            else
            {
                _logger.LogDebug("AppendBlob does not exist, creating new one");
                // Create the append blob with proper content type
                await appendBlobClient.CreateAsync(
                    new AppendBlobCreateOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "application/x-ndjson" },
                    }
                );
            }

            // Use overwrite parameter to control append vs overwrite behavior
            // overwrite: true = truncate and start fresh
            // overwrite: false = append to existing content
            var writeStream = await appendBlobClient.OpenWriteAsync(overwrite: !appendMode);

            _logger.LogInformation(
                "Successfully created write stream for blob: {BlobUri} (append mode: {AppendMode})",
                blobUri,
                appendMode
            );
            return writeStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get write stream for blob URI: {BlobUri} (append mode: {AppendMode})",
                blobUri,
                appendMode
            );
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

    private static AppendBlobClient CreateAppendBlobClient(Uri blobUri)
    {
        // Use managed identity for authentication (preferred for Azure-hosted applications)
        // Falls back to other credential types as appropriate
        var credential = new DefaultAzureCredential();

        return new AppendBlobClient(blobUri, credential);
    }
}
