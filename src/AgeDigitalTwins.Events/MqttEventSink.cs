using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public class MqttEventSink : IEventSink
{
    public async Task SendEventAsync(CloudEvent cloudEvent)
    {
        // Implement MQTT sending logic here
    }
}
