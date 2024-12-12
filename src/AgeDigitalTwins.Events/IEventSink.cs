using System.Text.Json.Serialization;
using CloudNative.CloudEvents;
using Json.More;

namespace AgeDigitalTwins.Events;

public interface IEventSink
{
    string Name { get; }
    Task SendEventsAsync(IEnumerable<CloudEvent> cloudEvents);
}

[JsonConverter(typeof(EnumStringConverter<EventType>))]
public enum EventFormat
{
    EventNotification,
    DataHistory,
}

public class EventRoute
{
    public required string SinkName { get; set; }
    public EventFormat? EventFormat { get; set; }
    public List<EventType>? EventTypes { get; set; }
}
