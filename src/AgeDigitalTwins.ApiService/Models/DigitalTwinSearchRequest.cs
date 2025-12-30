namespace AgeDigitalTwins.ApiService.Models;

public record DigitalTwinSearchRequest(
    double[] Vector,
    string? EmbeddingProperty,
    string? ModelFilter,
    int? Limit
);
