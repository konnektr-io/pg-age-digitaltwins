using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Metadata about the model a digital twin conforms to.
/// Based on Azure.DigitalTwins.Core.DigitalTwinMetadata (MIT License).
/// </summary>
[JsonConverter(typeof(DigitalTwinMetadataJsonConverter))]
public class DigitalTwinMetadata
{
    /// <summary>
    /// The Id of the model that the digital twin conforms to.
    /// </summary>
    [JsonPropertyName("$model")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Model-defined writable properties' metadata.
    /// </summary>
    [JsonPropertyName("$metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, DigitalTwinPropertyMetadata>? PropertyMetadata { get; set; }
}

/// <summary>
/// Metadata about a property on a digital twin.
/// Based on Azure.DigitalTwins.Core.DigitalTwinPropertyMetadata (MIT License).
/// </summary>
public class DigitalTwinPropertyMetadata
{
    /// <summary>
    /// The date and time the property was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastUpdatedOn { get; set; }

    /// <summary>
    /// The source time when the property was updated (optional, user-provided).
    /// </summary>
    [JsonPropertyName("sourceTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? SourceTime { get; set; }
}
