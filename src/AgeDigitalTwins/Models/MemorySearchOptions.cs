using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AgeDigitalTwins.Exceptions;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Options for a scoped vector memory search over digital twins.
/// All scope predicates (model allow-list, property equality filters, related-twin
/// predicate) are applied in the database <b>before</b> nearest-neighbour ranking
/// and <c>LIMIT</c>, so the returned ranking never sees out-of-scope records.
/// </summary>
/// <remarks>
/// Scope attributes (for example a tenant, workspace, or owner key) are plain twin
/// properties managed by the caller: store them on the twin and pass them back as
/// <see cref="PropertyFilters"/>. This API performs equality filtering only — it does
/// not enforce ownership or authorization. Cross-graph fan-out and owner-level access
/// control must be handled outside this API until granular twin permissions exist.
/// </remarks>
public sealed class MemorySearchOptions
{
    /// <summary>Maximum number of results. Bounded to 1..100.</summary>
    public const int MaxLimit = 100;

    /// <summary>
    /// Maximum supported query-vector dimensionality (HNSW index limit).
    /// </summary>
    public const int MaxVectorDimensions = 2000;

    private static readonly Regex IdentifierPattern =
        new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>The query embedding. Required, at least one dimension.</summary>
    public required double[] Vector { get; init; }

    /// <summary>
    /// Twin property holding the stored embedding. Defaults to "embedding".
    /// Must be a plain property identifier (no paths or expressions).
    /// </summary>
    public string EmbeddingProperty { get; init; } = "embedding";

    /// <summary>Maximum number of ranked records to return. Defaults to 10.</summary>
    public int Limit { get; init; } = 10;

    /// <summary>
    /// Optional allow-list of twin model IDs (<c>$metadata.$model</c>).
    /// When set, only twins conforming to one of these models are ranked.
    /// </summary>
    public IReadOnlyList<string>? ModelIds { get; init; }

    /// <summary>
    /// Optional caller-managed scope predicates as twin-property equality filters
    /// (for example <c>{ "workspaceId": "w-42" }</c>). Keys must be plain property
    /// identifiers; values are matched with equality semantics.
    /// </summary>
    public IReadOnlyDictionary<string, string>? PropertyFilters { get; init; }

    /// <summary>
    /// Optional twin ID. When set, only twins that share any relationship
    /// (in either direction) with this twin are ranked.
    /// </summary>
    public string? RelatedTwinId { get; init; }

    /// <summary>
    /// Optional expected embedding dimensionality. When set it must equal the
    /// query-vector length, and — when an HNSW index exists for the embedding
    /// property — the indexed dimension as well.
    /// </summary>
    public int? ExpectedDimension { get; init; }

    internal void Validate()
    {
        if (Vector == null || Vector.Length == 0)
        {
            throw new ValidationFailedException("Query vector must contain at least one dimension.");
        }

        if (Vector.Length > MaxVectorDimensions)
        {
            throw new ValidationFailedException(
                $"Query vector has {Vector.Length} dimensions, which exceeds the maximum of {MaxVectorDimensions}."
            );
        }

        if (Vector.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
        {
            throw new ValidationFailedException("Query vector must only contain finite numbers.");
        }

        if (string.IsNullOrWhiteSpace(EmbeddingProperty) || !IdentifierPattern.IsMatch(EmbeddingProperty))
        {
            throw new ValidationFailedException(
                $"Embedding property '{EmbeddingProperty}' is not a valid property identifier."
            );
        }

        if (Limit < 1 || Limit > MaxLimit)
        {
            throw new ValidationFailedException($"Limit must be between 1 and {MaxLimit}.");
        }

        if (ModelIds != null)
        {
            foreach (string modelId in ModelIds)
            {
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    throw new ValidationFailedException("Model allow-list must not contain empty model IDs.");
                }
            }
        }

        if (PropertyFilters != null)
        {
            foreach (var filter in PropertyFilters)
            {
                if (string.IsNullOrWhiteSpace(filter.Key) || !IdentifierPattern.IsMatch(filter.Key))
                {
                    throw new ValidationFailedException(
                        $"Property filter key '{filter.Key}' is not a valid property identifier."
                    );
                }

                if (filter.Value == null)
                {
                    throw new ValidationFailedException(
                        $"Property filter '{filter.Key}' must have a non-null value."
                    );
                }
            }
        }

        if (RelatedTwinId != null && string.IsNullOrWhiteSpace(RelatedTwinId))
        {
            throw new ValidationFailedException("Related twin ID must not be empty when provided.");
        }

        if (ExpectedDimension.HasValue)
        {
            if (ExpectedDimension.Value < 1 || ExpectedDimension.Value > MaxVectorDimensions)
            {
                throw new ValidationFailedException(
                    $"Expected dimension must be between 1 and {MaxVectorDimensions}."
                );
            }

            if (ExpectedDimension.Value != Vector.Length)
            {
                throw new ValidationFailedException(
                    $"Query vector has {Vector.Length} dimensions but {ExpectedDimension.Value} were expected."
                );
            }
        }
    }

    internal static bool IsValidIdentifier(string value) => IdentifierPattern.IsMatch(value);
}
