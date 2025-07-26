using System.Text.Json.Nodes;

namespace AgeDigitalTwins.Events;

public class EventData(string id, string graphName, string tableName, DateTime? timestamp = null)
{
    public string? Id { get; } = id;
    public string? GraphName { get; } = graphName;
    public string? TableName { get; } = tableName;
    public JsonObject? OldValue { get; set; }
    public JsonObject? NewValue { get; set; }
    public EventType? EventType { get; set; }
    public DateTime? Timestamp { get; } = timestamp ?? DateTime.UtcNow;
}
