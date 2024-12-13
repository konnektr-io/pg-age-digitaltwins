using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public interface IEventSink
{
    string Name { get; }
    Task SendEventsAsync(IEnumerable<CloudEvent> cloudEvents);
}
