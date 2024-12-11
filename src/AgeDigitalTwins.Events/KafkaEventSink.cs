using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Kafka;
using CloudNative.CloudEvents.SystemTextJson;
using Confluent.Kafka;

namespace AgeDigitalTwins.Events;

public class KafkaEventSink(KafkaSinkOptions options) : IEventSink
{
    private readonly KafkaSinkOptions _options = options;

    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();

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

        using var producer = new ProducerBuilder<string?, byte[]>(config).Build();

        try
        {
            var message = cloudEvent.ToKafkaMessage(ContentMode.Binary, _formatter);

            var result = await producer.ProduceAsync(_options.Topic, message);
            Console.WriteLine($"Delivered '{result.Value}' to '{result.TopicPartitionOffset}'");
        }
        catch (ProduceException<Null, string> e)
        {
            Console.WriteLine($"Delivery failed: {e.Error.Reason}");
        }
    }
}

public class KafkaSinkOptions
{
    public required string Name { get; set; }
    public required string BrokerList { get; set; }
    public required string Topic { get; set; }
    public string SecurityProtocol { get; set; }
    public string SaslMechanism { get; set; } // Can be PLAIN or OAUTHBEARER for entra id
    public string SaslUsername { get; set; }
    public string SaslPassword { get; set; }
}
