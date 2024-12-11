using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public interface IEventSink
{
    string Name { get; }
    Task SendEventAsync(CloudEvent cloudEvent);
}

public class EventRoute
{
    public string SinkName { get; set; }
    public string EventFormat { get; set; }
    public List<string> EventTypes { get; set; }
}
