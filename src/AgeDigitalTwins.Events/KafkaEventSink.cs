using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Kafka;
using CloudNative.CloudEvents.SystemTextJson;
using Confluent.Kafka;

namespace AgeDigitalTwins.Events;

public class KafkaEventSink : IEventSink, IDisposable
{
    public KafkaEventSink(KafkaSinkOptions options)
    {
        Name = options.Name;
        _topic = options.Topic;
        ProducerConfig config =
            new()
            {
                BootstrapServers = options.BrokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
            };
        _producer = new ProducerBuilder<string?, byte[]>(config).Build();
    }

    public string Name { get; }

    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();

    private readonly IProducer<string?, byte[]> _producer;

    private readonly string _topic;

    public async Task SendEventsAsync(IEnumerable<CloudEvent> cloudEvents)
    {
        foreach (var cloudEvent in cloudEvents)
        {
            try
            {
                Message<string?, byte[]> message = cloudEvent.ToKafkaMessage(
                    ContentMode.Binary,
                    _formatter
                );

                DeliveryResult<string?, byte[]> result = await _producer.ProduceAsync(
                    _topic,
                    message
                );
                Console.WriteLine($"Delivered '{result.Value}' to '{result.TopicPartitionOffset}'");
            }
            catch (ProduceException<Null, string> e)
            {
                Console.WriteLine($"Delivery failed: {e.Error.Reason}");
            }
        }
    }

    public void Dispose()
    {
        // Dispose the producer
        _producer.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class KafkaSinkOptions
{
    public required string Name { get; set; }
    public required string BrokerList { get; set; }
    public required string Topic { get; set; }
    public string? SecurityProtocol { get; set; }
    public string? SaslMechanism { get; set; } // Can be PLAIN or OAUTHBEARER for entra id
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
}