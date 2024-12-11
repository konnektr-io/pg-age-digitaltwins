using System.Text.Json;
using CloudNative.CloudEvents;
using Confluent.Kafka;

namespace AgeDigitalTwins.Events;

public class KafkaEventSink(KafkaSinkOptions options) : IEventSink
{
    private readonly KafkaSinkOptions _options = options;

    public string Name => _options.Name;

    public async Task SendEventAsync(CloudEvent cloudEvent)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BrokerList,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = _options.SaslUsername,
            SaslPassword = _options.SaslPassword,
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();
        var message = new Message<Null, string> { Value = JsonSerializer.Serialize(cloudEvent) };

        await producer.ProduceAsync(_options.Topic, message);
    }
}

public class KafkaSinkOptions
{
    public required string Name { get; set; }
    public required string BrokerList { get; set; }
    public required string Topic { get; set; }
    public string SecurityProtocol { get; set; }
    public string SaslMechanism { get; set; }
    public string SaslUsername { get; set; }
    public string SaslPassword { get; set; }
}
