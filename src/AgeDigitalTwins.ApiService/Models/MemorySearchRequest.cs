namespace AgeDigitalTwins.ApiService.Models;

/// <summary>
/// Request DTO for scoped vector memory search over digital twins.
/// All scope predicates are applied in the database before ranking and
/// <c>Limit</c>. Scope attributes are caller-managed twin properties: store them
/// on the twin and pass them back as <see cref="PropertyFilters"/>.
/// </summary>
public record MemorySearchRequest(
    /// <summary>Query embedding. Required.</summary>
    double[]? Vector,
    /// <summary>Twin property holding the stored embedding. Defaults to "embedding".</summary>
    string? EmbeddingProperty,
    /// <summary>Maximum ranked records to return (1..100, default 10).</summary>
    int? Limit,
    /// <summary>Optional allow-list of twin model IDs.</summary>
    string[]? ModelIds,
    /// <summary>Optional caller-managed scope predicates as twin-property equality filters.</summary>
    Dictionary<string, string>? PropertyFilters,
    /// <summary>Optional twin ID; only twins related to this twin are ranked.</summary>
    string? RelatedTwinId,
    /// <summary>Optional expected embedding dimensionality.</summary>
    int? ExpectedDimension,
    /// <summary>Maximum excerpt characters per record (default 500, max 4000).</summary>
    int? ExcerptLength
);
