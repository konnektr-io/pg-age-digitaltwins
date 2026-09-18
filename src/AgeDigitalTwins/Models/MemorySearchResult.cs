namespace AgeDigitalTwins.Models;

/// <summary>
/// A single ranked record from a scoped vector memory search.
/// </summary>
/// <param name="Twin">The matching twin.</param>
/// <param name="Distance">
/// L2 distance between the query vector and the twin's stored embedding.
/// Lower values rank first.
/// </param>
public sealed record MemorySearchResult(BasicDigitalTwin Twin, double Distance);
