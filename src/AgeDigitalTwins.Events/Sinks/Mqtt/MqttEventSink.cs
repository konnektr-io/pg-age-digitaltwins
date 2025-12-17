using System;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Events.Abstractions;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Mqtt;
using CloudNative.CloudEvents.SystemTextJson;
using MQTTnet;
using Azure.Core;

namespace AgeDigitalTwins.Events.Sinks.Mqtt;

public class MqttEventSink : IEventSink, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger _logger;
    private readonly string _topic;
    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();
    private bool _isHealthy = true;

    private readonly MqttSinkOptions _options;
    private readonly TokenCredential? _credential;

    public MqttEventSink(MqttSinkOptions options, TokenCredential? credential, ILogger logger)
    {
        Name = options.Name;
        _logger = logger;
        _topic = options.Topic;
        _options = options;
        _credential = credential;

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        ConnectAsync().Wait();
    }

    private async Task ConnectAsync()
    {
        string? password = _options.Password;
        string? username = _options.Username;

        if (string.Equals(_options.AuthenticationType, "OAuth", StringComparison.OrdinalIgnoreCase) && _credential != null)
        {
            try 
            {
                 var context = new TokenRequestContext(string.IsNullOrEmpty(_options.Scope) ? [] : [_options.Scope]);
                 var token = await _credential.GetTokenAsync(context, default);
                 password = token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve OAuth token for MQTT sink '{SinkName}'", Name);
                throw;
            }
        }

        var mqttOptions = new MqttClientOptionsBuilder()
            .WithProtocolVersion(_options.GetProtocolVersion())
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.Broker, _options.Port)
            .WithCredentials(username, password)
            .WithCleanSession()
            .Build();

        if (!_mqttClient.IsConnected)
        {
             await _mqttClient.ConnectAsync(mqttOptions);
        }
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
                // Re-connect logic needs to fetch fresh token if OAuth
                await ConnectAsync();
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

