namespace AgeDigitalTwins.ApiService.Models;

/// <summary>Request DTO for creating the HNSW index behind scoped vector memory search.</summary>
public record MemorySearchIndexRequest(
    /// <summary>Twin property holding the stored embedding. Defaults to "embedding".</summary>
    string? EmbeddingProperty,
    /// <summary>Dimensionality of the stored embeddings. Required.</summary>
    int? Dimension,
    /// <summary>Optional HNSW m parameter (2..100).</summary>
    int? M,
    /// <summary>Optional HNSW ef_construction parameter (4..1000).</summary>
    int? EfConstruction
);
