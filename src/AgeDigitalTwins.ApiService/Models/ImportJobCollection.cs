using AgeDigitalTwins.Jobs.Models;

namespace AgeDigitalTwins.ApiService.Models;

/// <summary>
/// A collection of import job objects, compatible with Azure Digital Twins API.
/// </summary>
public class ImportJobCollection
{
    /// <summary>
    /// Gets or sets the list of import job objects.
    /// </summary>
    public IList<ImportJobResult> Value { get; set; } = new List<ImportJobResult>();

    /// <summary>
    /// Gets or sets a URI to retrieve the next page of results.
    /// </summary>
    public string? NextLink { get; set; }
}
