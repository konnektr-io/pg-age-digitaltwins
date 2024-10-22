using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Api.Models;

[JsonConverter(typeof(BasicDigitalTwinJsonConverter))]
public class DigitalTwin
{
    /// <summary>
    /// The unique Id of the digital twin in a digital twins instance. This field is present on every digital twin.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinId)]
    public string Id { get; set; }

    /// <summary>
    /// A string representing a weak ETag for the entity that this request performs an operation against, as per RFC7232.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinETag)]
    public ETag? ETag { get; set; }

    /// <summary>
    /// The date and time the twin was last updated.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime)]
    public DateTimeOffset? LastUpdatedOn { get; internal set; }

    /// <summary>
    /// Information about the model a digital twin conforms to.
    /// This field is present on every digital twin.
    /// </summary>
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata)]
    public DigitalTwinMetadata Metadata { get; set; } = new DigitalTwinMetadata();

    /// <summary>
    /// This field will contain properties and components as defined in the contents section of the DTDL definition of the twin.
    /// </summary>
    /// <remarks>
    /// If the property is a component, use the <see cref="BasicDigitalTwinComponent"/> class to deserialize the payload.
    /// </remarks>
    /// <example>
    /// <code snippet="Snippet:DigitalTwinsSampleGetBasicDigitalTwin" language="csharp">
    /// Response&lt;BasicDigitalTwin&gt; getBasicDtResponse = await client.GetDigitalTwinAsync&lt;BasicDigitalTwin&gt;(basicDtId);
    /// BasicDigitalTwin basicDt = getBasicDtResponse.Value;
    ///
    /// // Must cast Component1 as a JsonElement and get its raw text in order to deserialize it as a dictionary
    /// string component1RawText = ((JsonElement)basicDt.Contents[&quot;Component1&quot;]).GetRawText();
    /// var component1 = JsonSerializer.Deserialize&lt;BasicDigitalTwinComponent&gt;(component1RawText);
    ///
    /// Console.WriteLine($&quot;Retrieved and deserialized digital twin {basicDt.Id}:\n\t&quot; +
    ///     $&quot;ETag: {basicDt.ETag}\n\t&quot; +
    ///     $&quot;ModelId: {basicDt.Metadata.ModelId}\n\t&quot; +
    ///     $&quot;LastUpdatedOn: {basicDt.LastUpdatedOn}\n\t&quot; +
    ///     $&quot;Prop1: {basicDt.Contents[&quot;Prop1&quot;]}, last updated on {basicDt.Metadata.PropertyMetadata[&quot;Prop1&quot;].LastUpdatedOn}\n\t&quot; +
    ///     $&quot;Prop2: {basicDt.Contents[&quot;Prop2&quot;]}, last updated on {basicDt.Metadata.PropertyMetadata[&quot;Prop2&quot;].LastUpdatedOn} and sourced at {basicDt.Metadata.PropertyMetadata[&quot;Prop2&quot;].SourceTime}\n\t&quot; +
    ///     $&quot;Component1.LastUpdatedOn: {component1.LastUpdatedOn}\n\t&quot; +
    ///     $&quot;Component1.Prop1: {component1.Contents[&quot;ComponentProp1&quot;]}, last updated on: {component1.Metadata[&quot;ComponentProp1&quot;].LastUpdatedOn}\n\t&quot; +
    ///     $&quot;Component1.Prop2: {component1.Contents[&quot;ComponentProp2&quot;]}, last updated on: {component1.Metadata[&quot;ComponentProp2&quot;].LastUpdatedOn} and sourced at: {component1.Metadata[&quot;ComponentProp2&quot;].SourceTime}&quot;);
    /// </code>
    /// </example>

    [JsonExtensionData]
    public IDictionary<string, object> Contents { get; set; } = new Dictionary<string, object>();
}