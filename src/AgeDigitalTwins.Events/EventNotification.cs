using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public class EventNotification
{
    public string TwinId { get; set; }
    public string RelationshipId { get; set; }
    public string NewValue { get; set; }
    public string OldValue { get; set; }
    public string JsonPatch { get; set; }
}
