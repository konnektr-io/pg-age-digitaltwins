using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Properties on a component within a digital twin.
/// Based on Azure.DigitalTwins.Core.Serialization.BasicDigitalTwinComponent (MIT License).
/// </summary>
[JsonConverter(typeof(BasicDigitalTwinComponentJsonConverter))]
public class BasicDigitalTwinComponent
{
    /// <summary>
    /// The date and time the component was last updated.
    /// </summary>
    [JsonPropertyName("$lastUpdateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastUpdatedOn { get; set; }

    /// <summary>
    /// Model-defined writable properties' metadata for the component.
    /// </summary>
    [JsonPropertyName("$metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, DigitalTwinPropertyMetadata>? Metadata { get; set; }

    /// <summary>
    /// The contents of the component (properties).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Contents { get; set; } = new();
}
