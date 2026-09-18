namespace AgeDigitalTwins.ApiService.Models;

/// <summary>A single ranked record from scoped vector memory search.</summary>
public record MemorySearchResultDto(
    /// <summary>Stable twin ID ($dtId).</summary>
    string Id,
    /// <summary>Twin model ID ($metadata.$model).</summary>
    string? ModelId,
    /// <summary>L2 distance to the query vector. Lower values rank first.</summary>
    double Distance,
    /// <summary>
    /// Bounded JSON excerpt of the twin's properties. The embedding vector itself
    /// is excluded; fetch the full twin via GET /digitaltwins/{id} when needed.
    /// </summary>
    string Excerpt,
    /// <summary>Last update time of the twin, when known.</summary>
    DateTimeOffset? LastUpdatedOn
);
