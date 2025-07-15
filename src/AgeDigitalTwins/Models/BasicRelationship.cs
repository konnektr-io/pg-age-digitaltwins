using System.Text.Json;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Represents a basic relationship for batch operations.
/// </summary>
public class BasicRelationship
{
    /// <summary>
    /// Gets or sets the source digital twin ID.
    /// </summary>
    public required string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the target digital twin ID.
    /// </summary>
    public required string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the relationship ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the relationship name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the relationship properties as JSON.
    /// </summary>
    public JsonElement Properties { get; set; } = JsonSerializer.SerializeToElement(new { });
}
