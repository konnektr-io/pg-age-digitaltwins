using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public class MqttEventSink(MqttSinkOptions options) : IEventSink
{
    private readonly MqttSinkOptions _options = options;

    public string Name => _options.Name;

    public async Task SendEventAsync(CloudEvent cloudEvent)
    {
        await Task.CompletedTask;
        // Implement MQTT sending logic here
    }
}

public class MqttSinkOptions
{
    public required string Name { get; set; }
    public required string Broker { get; set; }
    public required string Topic { get; set; }
}
