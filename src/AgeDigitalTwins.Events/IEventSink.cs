using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public interface IEventSink
{
    Task SendEventAsync(CloudEvent cloudEvent);
}
