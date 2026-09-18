namespace AgeDigitalTwins.ApiService.Models;

/// <summary>The HNSW index backing scoped vector memory search.</summary>
public record MemorySearchIndex(
    /// <summary>Name of the index (pre-existing when already created).</summary>
    string IndexName,
    /// <summary>Embedding dimensionality the index was built for.</summary>
    int Dimension
);
