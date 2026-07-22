using System.Text;
using System.Text.Json;
using AgeDigitalTwins.Events.Abstractions;
using Azure.Core;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

namespace AgeDigitalTwins.Events.Sinks.Nats;

public class NatsEventSink : IEventSink, IDisposable
{
    private readonly INatsClient _natsClient;
    private readonly INatsConnection _natsConnection;
    private readonly INatsJSContext? _jetStreamContext;
    private readonly ILogger _logger;
    private readonly string _subject;
    private readonly NatsSinkOptions _options;
    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();
    private readonly bool _ownsNatsClient;
    private bool _isHealthy = true;
    private bool _disposed;

    public NatsEventSink(
        NatsSinkOptions options,
        TokenCredential? credential,
        ILogger logger,
        INatsClient? natsClient = null,
        INatsJSContext? jetStreamContext = null
    )
    {
        Name = options.Name;
        _options = options;
        _logger = logger;
        _subject = options.Subject;

        try
        {
            _ownsNatsClient = natsClient is null;
            if (_ownsNatsClient)
            {
                var natsOpts = BuildNatsOpts(options, credential);
                _natsClient = new NatsClient(natsOpts);
            }
            else
            {
                _natsClient = natsClient!;
            }

            _natsConnection = _natsClient.Connection;
            _natsConnection.ConnectionDisconnected += OnDisconnected;
            _natsConnection.ConnectionOpened += OnReconnected;

            if (options.JetStream)
            {
                _jetStreamContext = jetStreamContext ?? new NatsJSContextFactory().CreateContext(_natsConnection);
            }

            _logger.LogInformation(
                "NATS event sink '{SinkName}' connected to {Url}",
                Name,
                options.Url
            );
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _logger.LogError(
                ex,
                "Failed to initialize NATS connection for sink '{SinkName}'",
                Name
            );
            throw;
        }
    }

    private ValueTask OnDisconnected(object? sender, NatsEventArgs e)
    {
        _logger.LogWarning("NATS sink '{SinkName}' disconnected: {Message}", Name, e.Message);
        return ValueTask.CompletedTask;
    }

    private ValueTask OnReconnected(object? sender, NatsEventArgs e)
    {
        _logger.LogInformation("NATS sink '{SinkName}' reconnected: {Message}", Name, e.Message);
        _isHealthy = true;
        return ValueTask.CompletedTask;
    }

    private static NatsOpts BuildNatsOpts(NatsSinkOptions options, TokenCredential? credential)
    {
        var natsOpts = NatsOpts.Default with
        {
            Url = options.Url,
        };

        if (!string.IsNullOrEmpty(options.Username) || !string.IsNullOrEmpty(options.Password))
        {
            natsOpts = natsOpts with
            {
                AuthOpts = new NatsAuthOpts
                {
                    Username = options.Username,
                    Password = options.Password,
                },
            };
        }

        if (!string.IsNullOrEmpty(options.Token))
        {
            natsOpts = natsOpts with
            {
                AuthOpts = new NatsAuthOpts
                {
                    Token = options.Token,
                },
            };
        }

        if (
            string.Equals(options.AuthenticationType, "OAuth", StringComparison.OrdinalIgnoreCase)
            && credential != null
        )
        {
            try
            {
                var context = new TokenRequestContext(
                    string.IsNullOrEmpty(options.Scope) ? [] : [options.Scope]
                );
                var token = credential.GetToken(context, default);
                natsOpts = natsOpts with
                {
                    AuthOpts = new NatsAuthOpts
                    {
                        Token = token.Token,
                    },
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve OAuth token for NATS sink '{options.Name}'",
                    ex
                );
            }
        }

        if (options.TlsEnabled)
        {
            natsOpts = natsOpts with
            {
                TlsOpts = new NatsTlsOpts
                {
                    InsecureSkipVerify = options.TlsSkipVerify,
                },
            };
        }

        return natsOpts;
    }

    public string Name { get; }

    public bool IsHealthy => _isHealthy;

    public async Task SendEventsAsync(
        IEnumerable<CloudEvent> cloudEvents,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var cloudEvent in cloudEvents)
        {
            try
            {
                byte[] data;
                NatsHeaders? headers = null;

                if (_options.UseBinaryMode)
                {
                    headers = new NatsHeaders();
                    headers.Add("ce-specversion", cloudEvent.SpecVersion.VersionId);
                    headers.Add("ce-type", cloudEvent.Type!);
                    headers.Add("ce-source", cloudEvent.Source!.ToString());
                    headers.Add("ce-id", cloudEvent.Id!);
                    if (cloudEvent.Time is DateTimeOffset time)
                        headers.Add("ce-time", time.ToString("o"));
                    if (cloudEvent.DataContentType is string contentType)
                        headers.Add("ce-datacontenttype", contentType);

                    if (cloudEvent.Data is byte[] byteData)
                        data = byteData;
                    else if (cloudEvent.Data is string strData)
                        data = Encoding.UTF8.GetBytes(strData);
                    else if (cloudEvent.Data is not null)
                        data = JsonSerializer.SerializeToUtf8Bytes(cloudEvent.Data);
                    else
                        data = [];
                }
                else
                {
                    headers = new NatsHeaders();
                    headers.Add("Content-Type", "application/cloudevents");
                    var bytes = _formatter.EncodeStructuredModeMessage(cloudEvent, out _);
                    data = bytes.ToArray();
                }

                if (_jetStreamContext != null)
                {
                    var ack = await _jetStreamContext.PublishAsync<byte[]>(
                        _subject,
                        data,
                        headers: headers,
                        cancellationToken: cancellationToken
                    );

                    if (ack.Error != null)
                    {
                        throw new NatsJSException(
                            $"JetStream publish failed: {ack.Error.Description} (code: {ack.Error.Code})"
                        );
                    }
                }
                else
                {
                    await _natsClient.PublishAsync<byte[]>(
                        _subject,
                        data,
                        headers,
                        cancellationToken: cancellationToken
                    );
                }

                _isHealthy = true;
                _logger.LogInformation(
                    "Published message {MessageId} of type {EventType} with source {EventSource} to NATS sink '{SinkName}' on subject '{Subject}'",
                    cloudEvent.Id,
                    cloudEvent.Type,
                    cloudEvent.Source,
                    Name,
                    _subject
                );
            }
            catch (Exception ex)
            {
                _isHealthy = false;
                _logger.LogError(
                    ex,
                    "Failed to publish event {EventId} to NATS sink '{SinkName}': {Reason}",
                    cloudEvent.Id,
                    Name,
                    ex.Message
                );
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_ownsNatsClient)
            return;

        try
        {
            if (_natsClient is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
            }
            else if (_natsClient is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error disposing NATS connection for sink '{SinkName}'",
                Name
            );
        }

        GC.SuppressFinalize(this);
    }
}
