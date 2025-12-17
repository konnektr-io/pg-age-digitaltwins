using System.Text.Json;
using System.Text.Json.Nodes;
using AgeDigitalTwins.Events.Abstractions;
using AgeDigitalTwins.Events.Core.Events;
using Npgsql;

namespace AgeDigitalTwins.Events.Core.Services;

/// <summary>
/// Service that listens for telemetry events via PostgreSQL NOTIFY
/// and forwards them to the shared event queue.
/// </summary>
public class TelemetryListener
{
    private readonly string _connectionString;
    private readonly IEventQueue _eventQueue;
    private readonly ILogger<TelemetryListener> _logger;
    private readonly string _channel = "digitaltwins_telemetry";

    public TelemetryListener(
        string connectionString,
        IEventQueue eventQueue,
        ILogger<TelemetryListener> logger
    )
    {
        _connectionString = connectionString;
        _eventQueue = eventQueue;
        _logger = logger;
    }

    /// <summary>
    /// Indicates whether the telemetry listener is currently healthy.
    /// </summary>
    public bool IsHealthy { get; private set; } = false;

    /// <summary>
    /// Starts listening for telemetry events.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Telemetry listener starting...");

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Set up notification handler
            connection.Notification += OnTelemetryReceived;

            // Listen to the telemetry channel
            await using var command = new NpgsqlCommand($"LISTEN {_channel}", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Listening for telemetry events on channel: {Channel}",
                _channel
            );

            // Mark as healthy now that listening has started successfully
            IsHealthy = true;

            // Keep the connection alive and listening
            while (!cancellationToken.IsCancellationRequested)
            {
                await connection.WaitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Telemetry listener stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in telemetry listener");
            throw;
        }
        finally
        {
            IsHealthy = false;
        }
    }

    private void OnTelemetryReceived(object sender, NpgsqlNotificationEventArgs e)
    {
        try
        {
            if (e.Channel != _channel)
                return;

            var telemetryEvent = JsonSerializer.Deserialize<JsonObject>(e.Payload);
            if (telemetryEvent == null)
            {
                _logger.LogWarning("Received invalid telemetry payload: {Payload}", e.Payload);
                return;
            }

            var digitalTwinId = telemetryEvent["digitalTwinId"]?.ToString();
            var eventType = telemetryEvent["eventType"]?.ToString();
            var messageId = telemetryEvent["messageId"]?.ToString();
            var timestamp = telemetryEvent["timestamp"]?.ToString();
            var componentName = telemetryEvent["componentName"]?.ToString();
            var graphName = telemetryEvent["graphName"]?.ToString();

            if (
                string.IsNullOrEmpty(digitalTwinId)
                || string.IsNullOrEmpty(messageId)
                || string.IsNullOrEmpty(graphName)
            )
            {
                _logger.LogWarning("Telemetry event missing required fields: {Payload}", e.Payload);
                return;
            }

            // Convert telemetry notification to EventData
            var eventData = new EventData(
                digitalTwinId,
                graphName, // Default graph name
                "telemetry"
            )
            {
                EventType = EventType.Telemetry,
                NewValue = telemetryEvent,
                Timestamp = DateTime.TryParse(timestamp, out var parsedTime)
                    ? parsedTime
                    : DateTime.UtcNow,
            };

            _logger.LogDebug(
                "Received {EventType} for twin {TwinId}{ComponentInfo} with message ID {MessageId}",
                eventType,
                digitalTwinId,
                componentName != null ? $", component {componentName}" : "",
                messageId
            );

            // Forward to shared event queue
            _eventQueue.Enqueue(eventData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry notification: {Payload}", e.Payload);
        }
    }
}
