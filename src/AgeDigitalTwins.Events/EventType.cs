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
