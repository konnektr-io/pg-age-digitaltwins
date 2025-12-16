using AgeDigitalTwins.Events.Abstractions;
using Azure.Core;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Kafka;
using CloudNative.CloudEvents.SystemTextJson;
using Confluent.Kafka;

namespace AgeDigitalTwins.Events.Sinks.Kafka;



public class KafkaEventSink : IEventSink, IDisposable
{
    private readonly ILogger _logger;

    private readonly IProducer<string?, byte[]> _producer;

    private readonly string _topic;

    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();

    private readonly TokenCredential? _credential;

    private bool _isHealthy = true;
    private string? _lastError;
    private DateTime? _lastErrorTime;

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
                // Event Hubs specific settings
                RequestTimeoutMs = 60000,
                MessageTimeoutMs = 300000,
                // Retry and reliability settings
                MessageSendMaxRetries = 5,
                RetryBackoffMaxMs = 1000,
                RetryBackoffMs = 100,
                // Connection settings
                SocketTimeoutMs = 60000,
                ConnectionsMaxIdleMs = 300000,
                // Performance settings optimized for batching
                BatchSize = 65536, // 64KB - larger batches for better throughput
                LingerMs = 10, // Wait up to 10ms to collect more messages
                // Producer buffer settings
                QueueBufferingMaxMessages = 10000, // Allow more messages in producer queue
                QueueBufferingMaxKbytes = 524288, // 512MB producer buffer
            };

        if (Enum.TryParse<SecurityProtocol>(options.SecurityProtocol, true, out var securityProtocol))
        {
            config.SecurityProtocol = securityProtocol;
        }

        if (saslMechanism == SaslMechanism.Plain)
        {
            logger.LogDebug("Using SASL/PLAIN authentication for Kafka sink '{SinkName}'", Name);
            config.SaslUsername = options.SaslUsername;
            config.SaslPassword = options.SaslPassword;
            _producer = new ProducerBuilder<string?, byte[]>(config)
                .SetErrorHandler(
                    (_, e) =>
                    {
                        logger.LogError("Kafka producer error: {Error}", e.Reason);
                        _isHealthy = false;
                        _lastError = e.Reason;
                        _lastErrorTime = DateTime.UtcNow;
                    }
                )
                .SetLogHandler(
                    (_, logMessage) =>
                    {
                        // Only log warnings and errors to reduce noise
                        if (logMessage.Level <= SyslogLevel.Warning)
                        {
                            logger.LogWarning(
                                "Kafka log [{Level}]: {Message}",
                                logMessage.Level,
                                logMessage.Message
                            );
                        }
                    }
                )
                .Build();
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
            // This is passed to the TokenRefreshHandler
            // and used to get the token from Azure AD
            // We pass in the scope for the Event Hubs namespace
            config.SaslOauthbearerConfig =
                $"https://{options.BrokerList.Replace(":9093", "")}/.default";
            _producer = new ProducerBuilder<string?, byte[]>(config)
                .SetOAuthBearerTokenRefreshHandler(TokenRefreshHandler)
                .SetErrorHandler(
                    (_, e) =>
                    {
                        logger.LogError("Kafka producer error: {Error}", e.Reason);
                        _isHealthy = false;
                        _lastError = e.Reason;
                        _lastErrorTime = DateTime.UtcNow;
                    }
                )
                .SetLogHandler(
                    (_, logMessage) =>
                    {
                        // Only log warnings and errors to reduce noise
                        if (logMessage.Level <= SyslogLevel.Warning)
                        {
                            logger.LogWarning(
                                "Kafka log [{Level}]: {Message}",
                                logMessage.Level,
                                logMessage.Message
                            );
                        }
                    }
                )
                .Build();
        }
        else
        {
            throw new InvalidOperationException($"Invalid SaslMechanism for Kafka sink {Name}");
        }
    }

    public string Name { get; }

    /// <summary>
    /// Indicates whether the Kafka producer is healthy and able to send events.
    /// </summary>
    public bool IsHealthy => _isHealthy;

    public async Task SendEventsAsync(
        IEnumerable<CloudEvent> cloudEvents,
        CancellationToken cancellationToken = default
    )
    {
        var eventsList = cloudEvents.ToList();
        _logger.LogDebug(
            "Sending {EventCount} events to Kafka sink '{SinkName}'",
            eventsList.Count,
            Name
        );

        // Option 1: Fire-and-forget for maximum throughput (recommended for high volume)
        var tasks = new List<Task<DeliveryResult<string?, byte[]>>>();

        foreach (var cloudEvent in eventsList)
        {
            try
            {
                Message<string?, byte[]> message = cloudEvent.ToKafkaMessage(
                    ContentMode.Binary,
                    _formatter
                );

                // Start the async operation without awaiting - allows batching
                var task = _producer.ProduceAsync(_topic, message, cancellationToken);
                tasks.Add(task);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Error preparing message {MessageId} for Kafka sink '{SinkName}'",
                    cloudEvent.Id,
                    Name
                );
            }
        }

        // Wait for all messages to be sent (with overall timeout)
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            var results = await Task.WhenAll(tasks).WaitAsync(cts.Token);

            // Log success details
            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                var cloudEvent = eventsList[i];

                _logger.LogDebug(
                    "Delivered message {MessageId} of type {EventType} to partition {Partition}, offset {Offset}",
                    cloudEvent.Id,
                    cloudEvent.Type,
                    result.Partition.Value,
                    result.Offset.Value
                );
            }

            _logger.LogInformation(
                "Successfully sent {SuccessCount}/{TotalCount} events with source {EventSource} to Kafka sink '{SinkName}'",
                results.Length,
                eventsList.Count,
                eventsList.FirstOrDefault()?.Source?.ToString(),
                Name
            );

            _isHealthy = true; // Mark as healthy on successful send
        }
        catch (OperationCanceledException)
        {
            _isHealthy = false;
            _lastError = "Batch send operation timed out after 5 minutes";
            _lastErrorTime = DateTime.UtcNow;
            _logger.LogError(
                "Batch send operation timed out after 5 minutes for Kafka sink '{SinkName}'",
                Name
            );

            // Log individual task statuses
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                var cloudEvent = eventsList[i];

                if (task.IsCompletedSuccessfully)
                {
                    _logger.LogDebug("Message {MessageId} completed successfully", cloudEvent.Id);
                }
                else if (task.IsFaulted)
                {
                    _logger.LogError(task.Exception, "Message {MessageId} failed", cloudEvent.Id);
                }
                else
                {
                    _logger.LogWarning("Message {MessageId} still pending", cloudEvent.Id);
                }
            }
        }
        catch (Exception e)
        {
            _isHealthy = false;
            _lastError = e.Message;
            _lastErrorTime = DateTime.UtcNow;
            _logger.LogError(
                e,
                "Unexpected error during batch send to Kafka sink '{SinkName}'",
                Name
            );
        }
    }

    private void TokenRefreshHandler(IProducer<string?, byte[]> producer, string scope)
    {
        if (_credential == null)
        {
            _isHealthy = false;
            _lastError = "No credential provided";
            _lastErrorTime = DateTime.UtcNow;
            producer.OAuthBearerSetTokenFailure("No credential provided");
            return;
        }

        TokenRequestContext request = new TokenRequestContext(new[] { scope });

        try
        {
            var token = _credential.GetToken(request, default);
            producer.OAuthBearerSetToken(
                token.Token,
                token.ExpiresOn.ToUnixTimeMilliseconds(),
                "AzureCredential"
            );
            _isHealthy = true; // Mark as healthy on successful token refresh
        }
        catch (Exception e)
        {
            _isHealthy = false;
            _lastError = e.Message;
            _lastErrorTime = DateTime.UtcNow;
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

