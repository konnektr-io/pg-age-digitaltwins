using AgeDigitalTwins.Events.Abstractions;

namespace AgeDigitalTwins.Events.Core.Events;

public class EventRoute
{
    public required string SinkName { get; set; }
    public EventFormat? EventFormat { get; set; }
    public Dictionary<SinkEventType, string>? TypeMappings { get; set; }
}
