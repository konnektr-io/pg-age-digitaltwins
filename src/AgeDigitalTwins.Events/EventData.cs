using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.More;

namespace AgeDigitalTwins.Events;

[JsonConverter(typeof(EnumStringConverter<EventType>))]
public enum EventType
{
    TwinCreate,
    TwinUpdate,
    TwinDelete,
    RelationshipCreate,
    RelationshipUpdate,
    RelationshipDelete,
}

public class EventData
{
    public string? GraphName { get; set; }
    public string? TableName { get; set; }
    public JsonObject? OldValue { get; set; }
    public JsonObject? NewValue { get; set; }
    public EventType? EventType { get; set; }
    public DateTime? Timestamp { get; set; }
}
