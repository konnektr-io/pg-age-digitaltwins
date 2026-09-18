using AgeDigitalTwins.Exceptions;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Options for creating (idempotently) the HNSW index backing scoped vector
/// memory search for one twin embedding property.
/// </summary>
public sealed class MemorySearchIndexOptions
{
    /// <summary>
    /// Twin property holding the stored embedding. Defaults to "embedding".
    /// </summary>
    public string EmbeddingProperty { get; init; } = "embedding";

    /// <summary>
    /// Dimensionality of the stored embeddings (the <c>vector(n)</c> dimension).
    /// Must match the length of every query vector searched against this index.
    /// </summary>
    public required int Dimension { get; init; }

    /// <summary>Optional HNSW <c>m</c> parameter (2..100). Omit for the server default.</summary>
    public int? M { get; init; }

    /// <summary>
    /// Optional HNSW <c>ef_construction</c> parameter (4..1000). Omit for the server default.
    /// </summary>
    public int? EfConstruction { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(EmbeddingProperty) || !MemorySearchOptions.IsValidIdentifier(EmbeddingProperty))
        {
            throw new ValidationFailedException(
                $"Embedding property '{EmbeddingProperty}' is not a valid property identifier."
            );
        }

        if (Dimension < 1 || Dimension > MemorySearchOptions.MaxVectorDimensions)
        {
            throw new ValidationFailedException(
                $"Index dimension must be between 1 and {MemorySearchOptions.MaxVectorDimensions}."
            );
        }

        if (M.HasValue && (M.Value < 2 || M.Value > 100))
        {
            throw new ValidationFailedException("HNSW m must be between 2 and 100.");
        }

        if (EfConstruction.HasValue && (EfConstruction.Value < 4 || EfConstruction.Value > 1000))
        {
            throw new ValidationFailedException("HNSW ef_construction must be between 4 and 1000.");
        }
    }
}
