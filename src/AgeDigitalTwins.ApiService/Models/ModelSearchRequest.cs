namespace AgeDigitalTwins.ApiService.Models;

// Request DTO for model search
public record ModelSearchRequest(string? Query, double[]? Vector, int? Limit);
