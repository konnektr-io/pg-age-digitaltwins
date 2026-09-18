namespace AgeDigitalTwins.ApiService.Models;

/// <summary>Capability report for scoped vector memory search.</summary>
/// <param name="VectorSearchAvailable">
/// True when the pgvector extension is installed and memory search can be served.
/// </param>
public record MemorySearchCapability(bool VectorSearchAvailable);
