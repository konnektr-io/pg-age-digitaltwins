using System.Text.Json.Serialization;
using Json.More;

namespace AgeDigitalTwins.Events;

[JsonConverter(typeof(EnumStringConverter<EventFormat>))]
public enum EventFormat
{
    EventNotification,
    DataHistory,
}
