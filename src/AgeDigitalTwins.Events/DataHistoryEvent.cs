using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public class DataHistoryEvent
{
    public string TwinId { get; set; }
    public string RelationshipId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
}
