namespace AgeDigitalTwins.Events;

public abstract class SinkOptions
{
    public required string Name { get; set; }
    public Dictionary<SinkEventType, string>? EventTypeMappings { get; set; }
}
