using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Mqtt;
using CloudNative.CloudEvents.SystemTextJson;
using MQTTnet;
using MQTTnet.Formatter;

namespace AgeDigitalTwins.Events;

public class MqttEventSink : IEventSink, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger _logger;
    private readonly string _topic;
    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();

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

    public async Task SendEventsAsync(IEnumerable<CloudEvent> cloudEvents)
    {
        if (_mqttClient.IsConnected == false)
        {
            await _mqttClient.ReconnectAsync();
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

                await _mqttClient.PublishAsync(message);
                _logger.LogDebug("Published message to '{Topic}'", _topic);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Publishing failed: {Reason}", e.Message);
            }
        }
    }

    public void Dispose()
    {
        _mqttClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class MqttSinkOptions
{
    public required string Name { get; set; }
    public required string Broker { get; set; }
    public required int Port { get; set; }
    public required string Topic { get; set; }
    public required string ClientId { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public string ProtocolVersion { get; set; } = "5.0.0";

    public MqttProtocolVersion GetProtocolVersion()
    {
        return ProtocolVersion switch
        {
            "3.1.0" => MqttProtocolVersion.V310,
            "3.1.1" => MqttProtocolVersion.V311,
            "5.0.0" => MqttProtocolVersion.V500,
            _ => MqttProtocolVersion.Unknown,
        };
    }
}
