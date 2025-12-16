using AgeDigitalTwins.Events.Abstractions;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Mqtt;
using CloudNative.CloudEvents.SystemTextJson;
using MQTTnet;

namespace AgeDigitalTwins.Events.Sinks.Mqtt;

public class MqttEventSink : IEventSink, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger _logger;
    private readonly string _topic;
    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();
    private bool _isHealthy = true;

    public MqttEventSink(MqttSinkOptions options, ILogger logger)
    {
        Name = options.Name;
        _logger = logger;
        _topic = options.Topic;

        var mqttOptions = new MqttClientOptionsBuilder()
            .WithProtocolVersion(options.GetProtocolVersion())
            .WithClientId(options.ClientId)
            .WithTcpServer(options.Broker, options.Port)
            .WithCredentials(options.Username, options.Password)
            .WithCleanSession()
            .Build();

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        _mqttClient.ConnectAsync(mqttOptions).Wait();
    }

    public string Name { get; }

    /// <summary>
    /// Indicates whether the MQTT client is healthy and able to send events.
    /// </summary>
    public bool IsHealthy => _isHealthy && _mqttClient.IsConnected;

    public async Task SendEventsAsync(
        IEnumerable<CloudEvent> cloudEvents,
        CancellationToken cancellationToken = default
    )
    {
        if (_mqttClient.IsConnected == false)
        {
            try
            {
                await _mqttClient.ReconnectAsync(cancellationToken: cancellationToken);
                _isHealthy = true;
            }
            catch (Exception e)
            {
                _isHealthy = false;
                _logger.LogError(e, "Failed to reconnect MQTT client for {SinkName}", Name);
                throw;
            }
        }

        foreach (var cloudEvent in cloudEvents)
        {
            try
            {
                MqttApplicationMessage message = cloudEvent.ToMqttApplicationMessage(
                    ContentMode.Structured,
                    _formatter,
                    _topic
                );

                await _mqttClient.PublishAsync(message, cancellationToken);
                _isHealthy = true;
                _logger.LogInformation(
                    "Published message {MessageId} of type {EventType} with source {EventSource} to sink '{SinkName}' on topic '{Topic}'",
                    cloudEvent.Id,
                    cloudEvent.Type,
                    cloudEvent.Source,
                    Name,
                    _topic
                );
            }
            catch (Exception e)
            {
                _isHealthy = false;
                _logger.LogError(e, "Publishing failed for {SinkName}: {Reason}", Name, e.Message);
            }
        }
    }

    public void Dispose()
    {
        _mqttClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

