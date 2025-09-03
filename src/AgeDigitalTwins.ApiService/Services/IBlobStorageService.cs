namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Service for handling blob storage operations.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Gets a read stream from a blob URI.
    /// </summary>
    /// <param name="blobUri">The blob URI.</param>
    /// <returns>A stream for reading from the blob.</returns>
    Task<Stream> GetReadStreamAsync(Uri blobUri);

    /// <summary>
    /// Gets a write stream to a blob URI.
    /// </summary>
    /// <param name="blobUri">The blob URI.</param>
    /// <returns>A stream for writing to the blob.</returns>
    Task<Stream> GetWriteStreamAsync(Uri blobUri);

    /// <summary>
    /// Gets a write stream to a blob URI with specified write mode.
    /// </summary>
    /// <param name="blobUri">The blob URI.</param>
    /// <param name="appendMode">If true, appends to existing blob; if false, overwrites the blob.</param>
    /// <returns>A stream for writing to the blob.</returns>
    Task<Stream> GetWriteStreamAsync(Uri blobUri, bool appendMode);
}
