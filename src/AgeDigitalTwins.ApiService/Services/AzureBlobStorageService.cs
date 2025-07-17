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
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri.AbsoluteUri);

        try
        {
            _logger.LogDebug("Getting write stream for blob URI: {BlobUri}", blobUri);

            var blobClient = CreateBlobClient(blobUri);

            // Use OpenWriteAsync to get a stream that directly writes to blob storage
            var writeStream = await blobClient.OpenWriteAsync(overwrite: true);

            _logger.LogInformation(
                "Successfully created write stream for blob: {BlobUri}",
                blobUri
            );
            return writeStream;
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
}
