using System.Text.Json.Nodes;

namespace AgeDigitalTwins.Events;

public class EventData
{
    public string? GraphName { get; set; }
    public string? TableName { get; set; }
    public JsonObject? OldValue { get; set; }
    public JsonObject? NewValue { get; set; }
    public EventType? EventType { get; set; }
    public DateTime? Timestamp { get; set; }
}
