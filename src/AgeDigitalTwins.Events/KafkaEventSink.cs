using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public class KafkaEventSink : IEventSink
{
    public async Task SendEventAsync(CloudEvent cloudEvent)
    {
        // Implement Kafka sending logic here
    }
}
