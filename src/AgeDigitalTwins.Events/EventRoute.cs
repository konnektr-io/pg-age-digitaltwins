namespace AgeDigitalTwins.Events;

public class EventRoute
{
    public required string SinkName { get; set; }
    public EventFormat? EventFormat { get; set; }
    public List<EventType>? EventTypes { get; set; }
}
