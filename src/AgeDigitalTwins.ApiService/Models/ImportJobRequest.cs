using System.Text.Json.Serialization;

namespace AgeDigitalTwins.ApiService.Models;

/// <summary>
/// Represents a request to create an import job.
/// </summary>
public class ImportJobRequest
{
    /// <summary>
    /// Gets or sets the URI of the input blob containing the data to import.
    /// </summary>
    [JsonPropertyName("inputBlobUri")]
    public Uri InputBlobUri { get; set; } = default!;

    /// <summary>
    /// Gets or sets the URI of the output blob where the import log will be written.
    /// </summary>
    [JsonPropertyName("outputBlobUri")]
    public Uri OutputBlobUri { get; set; } = default!;
}
