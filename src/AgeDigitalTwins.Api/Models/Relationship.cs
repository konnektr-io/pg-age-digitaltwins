using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Api.Models;

public class Relationship
{
    /// <summary>
    /// The unique Id of the relationship. This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipId)]
    public string Id { get; set; }

    /// <summary>
    /// The unique Id of the target digital twin. This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipTargetId)]
    public string TargetId { get; set; }

    /// <summary>
    /// The unique Id of the source digital twin. This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipSourceId)]
    public string SourceId { get; set; }

    /// <summary>
    /// The name of the relationship, which defines the type of link (e.g. Contains). This field is present on every relationship.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.RelationshipName)]
    public string Name { get; set; }

    /// <summary>
    /// A string representing a weak ETag for the entity that this request performs an operation against, as per RFC7232.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinETag)]
    [JsonConverter(typeof(OptionalETagConverter))] // TODO: Remove when #16272 is fixed
    public ETag? ETag { get; set; }

    /// <summary>
    /// Additional, custom properties defined in the DTDL model.
    /// This property will contain any relationship properties that are not already defined in this class.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}