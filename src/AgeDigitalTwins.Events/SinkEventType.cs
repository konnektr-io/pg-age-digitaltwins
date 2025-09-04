using System.Text.Json.Serialization;
using Json.More;

namespace AgeDigitalTwins.Events
{
    [JsonConverter(typeof(EnumStringConverter<SinkEventType>))]
    public enum SinkEventType
    {
        TwinCreate,
        TwinUpdate,
        TwinDelete,
        RelationshipCreate,
        RelationshipUpdate,
        RelationshipDelete,
        Telemetry,
        PropertyEvent,
        TwinLifecycle,
        RelationshipLifecycle,
    }
}
