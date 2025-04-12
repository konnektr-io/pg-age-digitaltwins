using Azure.Core;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Kafka;
using CloudNative.CloudEvents.SystemTextJson;
using Confluent.Kafka;

namespace AgeDigitalTwins.Events;

public class KafkaEventSink : IEventSink, IDisposable
{
    private readonly ILogger _logger;

    private readonly IProducer<string?, byte[]> _producer;

    private readonly string _topic;

    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();

    private readonly TokenCredential? _credential;

    public KafkaEventSink(KafkaSinkOptions options, TokenCredential? credential, ILogger logger)
    {
        Name = options.Name;
        _credential = credential;
        _logger = logger;
        _topic = options.Topic;
        string bootstrapServers = options.BrokerList.EndsWith(":9093")
            ? options.BrokerList
            : options.BrokerList + ":9093";
        SaslMechanism saslMechanism = Enum.TryParse<SaslMechanism>(
            options.SaslMechanism,
            true,
            out var mechanism
        )
            ? mechanism
            : SaslMechanism.Plain;

        ProducerConfig config =
            new()
            {
                BootstrapServers = bootstrapServers,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = saslMechanism,
            };

        if (saslMechanism == SaslMechanism.Plain)
        {
            logger.LogDebug("Using SASL/PLAIN authentication for Kafka sink '{SinkName}'", Name);
            config.SaslUsername = options.SaslUsername;
            config.SaslPassword = options.SaslPassword;
            _producer = new ProducerBuilder<string?, byte[]>(config).Build();
        }
        // OAuth currently only supported for Azure Event Hubs
        else if (
            saslMechanism == SaslMechanism.OAuthBearer
            && bootstrapServers.Contains("servicebus")
        )
        {
            logger.LogDebug(
                "Using OAUTHBEARER (Azure) authentication for Kafka sink '{SinkName}'",
                Name
            );
            config.SaslOauthbearerConfig =
                $"https://{options.BrokerList.Replace(":9093", "")}/.default";
            _producer = new ProducerBuilder<string?, byte[]>(config)
                .SetOAuthBearerTokenRefreshHandler(TokenRefreshHandler)
                .Build();
        }
        else
        {
            throw new InvalidOperationException($"Invalid SaslMechanism for Kafka sink {Name}");
        }
    }

    public string Name { get; }

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

                _logger.LogDebug(
                    "Delivered message to '{TopicPartitionOffset}'",
                    result.TopicPartitionOffset
                );
            }
            catch (ProduceException<Null, string> e)
            {
                _logger.LogError(
                    e,
                    "Delivery failed for {SinkName}: {Reason}",
                    Name,
                    e.Error.Reason
                );
            }
        }
    }

    private void TokenRefreshHandler(IProducer<string?, byte[]> producer, string config)
    {
        if (_credential == null)
        {
            producer.OAuthBearerSetTokenFailure("No credential provided");
            return;
        }

        TokenRequestContext request = new([config]);

        try
        {
            var token = _credential.GetToken(request, default);
            producer.OAuthBearerSetToken(
                token.Token,
                token.ExpiresOn.ToUnixTimeMilliseconds(),
                "AzureCredential"
            );
        }
        catch (Exception e)
        {
            producer.OAuthBearerSetTokenFailure(e.Message);
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
    public string? SaslMechanism { get; set; } // Can be PLAIN or OAUTHBEARER for entra id
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
}
