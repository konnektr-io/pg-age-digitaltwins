using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// A helper class for deserializing and serializing digital twins.
/// Based on Azure.DigitalTwins.Core.BasicDigitalTwin (MIT License).
/// Compatible with Azure Digital Twins API but without external dependencies.
/// </summary>
/// <remarks>
/// This class provides a convenient way to work with digital twins without
/// needing to reference the Azure Digital Twins SDK. It maintains compatibility
/// with the Azure Digital Twins API surface.
/// </remarks>
[JsonConverter(typeof(BasicDigitalTwinJsonConverter))]
public class BasicDigitalTwin
{
    /// <summary>
    /// The unique Id of the digital twin in a digital twins instance.
    /// This field is present on every digital twin.
    /// </summary>
    [JsonPropertyName("$dtId")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// A string representing a weak ETag for the entity.
    /// </summary>
    [JsonPropertyName("$etag")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ETag { get; set; }

    /// <summary>
    /// The date and time the twin was last updated.
    /// </summary>
    [JsonPropertyName("$lastUpdateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastUpdatedOn { get; set; }

    /// <summary>
    /// Information about the model a digital twin conforms to.
    /// This field is present on every digital twin.
    /// </summary>
    [JsonPropertyName("$metadata")]
    public DigitalTwinMetadata Metadata { get; set; } = new();

    /// <summary>
    /// This field will contain properties and components as defined in the contents
    /// section of the DTDL definition of the twin.
    /// </summary>
    /// <remarks>
    /// Properties are stored as key-value pairs where the key is the property name
    /// and the value is the property value. For components, use the
    /// <see cref="BasicDigitalTwinComponent"/> class to deserialize the payload.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, object> Contents { get; set; } = new();
}
