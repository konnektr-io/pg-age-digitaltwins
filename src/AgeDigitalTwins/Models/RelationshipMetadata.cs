using System;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Metadata about a relationship, including tracking information.
/// Based on the pattern of <see cref="DigitalTwinMetadata"/> but without
/// model or property metadata fields that are not applicable to relationships.
/// </summary>
public class RelationshipMetadata
{
    /// <summary>
    /// The ID of the user who last updated the relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastUpdatedBy { get; set; }

    /// <summary>
    /// The date and time the relationship was last updated.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastUpdatedOn { get; set; }
}
