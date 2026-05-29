using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// A helper class for deserializing and serializing relationships.
/// Based on Azure.DigitalTwins.Core.BasicRelationship (MIT License).
/// Compatible with Azure Digital Twins API but without external dependencies.
/// </summary>
/// <remarks>
/// Although relationships have a user-defined schema, these properties should exist
/// on every instance. This is useful to use as a base class to ensure your custom
/// relationships have the necessary properties.
/// </remarks>
public class BasicRelationship
{
    /// <summary>
    /// The unique Id of the relationship.
    /// This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipId)]
    public string? Id { get; set; } = string.Empty;

    /// <summary>
    /// The unique Id of the target digital twin.
    /// This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipTargetId)]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// The unique Id of the source digital twin.
    /// This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipSourceId)]
    public string? SourceId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the relationship, which defines the type of link (e.g. Contains).
    /// This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipName)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A string representing a weak ETag for the entity.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinETag)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ETag { get; set; }

    /// <summary>
    /// Additional, custom properties defined in the DTDL model.
    /// This property will contain any relationship properties that are not
    /// already defined in this class.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Properties { get; set; } = new();
}
