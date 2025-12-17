using System.Text.Json.Serialization;
using Json.More;

namespace AgeDigitalTwins.Events.Core.Events;

[JsonConverter(typeof(EnumStringConverter<EventFormat>))]
public enum EventFormat
{
    EventNotification,
    DataHistory,
    Telemetry,
}
