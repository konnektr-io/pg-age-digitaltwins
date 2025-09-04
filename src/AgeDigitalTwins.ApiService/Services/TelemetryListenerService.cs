using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Background service that listens for telemetry events via PostgreSQL NOTIFY
/// and can forward them to configured endpoints or process them as needed.
/// </summary>
public class TelemetryListenerService : BackgroundService
{
    private readonly NpgsqlMultiHostDataSource _dataSource;
    private readonly ILogger<TelemetryListenerService> _logger;
    private readonly string _channel = "digitaltwins_telemetry";

    public TelemetryListenerService(
        NpgsqlMultiHostDataSource dataSource,
        ILogger<TelemetryListenerService> logger
    )
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry listener service starting...");

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.PreferStandby,
                stoppingToken
            );

            // Set up notification handler
            connection.Notification += OnTelemetryReceived;

            // Listen to the telemetry channel
            await using var command = new NpgsqlCommand($"LISTEN {_channel}", connection);
            await command.ExecuteNonQueryAsync(stoppingToken);

            _logger.LogInformation("Listening for telemetry events on channel: {Channel}", _channel);

            // Keep the connection alive and listening
            while (!stoppingToken.IsCancellationRequested)
            {
                await connection.WaitAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Telemetry listener service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in telemetry listener service");
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

            if (eventType == "Telemetry")
            {
                _logger.LogInformation(
                    "Received telemetry for twin {TwinId} with message ID {MessageId} at {Timestamp}",
                    digitalTwinId,
                    messageId,
                    timestamp
                );
            }
            else if (eventType == "ComponentTelemetry")
            {
                _logger.LogInformation(
                    "Received component telemetry for twin {TwinId}, component {ComponentName} with message ID {MessageId} at {Timestamp}",
                    digitalTwinId,
                    componentName,
                    messageId,
                    timestamp
                );
            }

            // TODO: Forward to configured sinks/endpoints
            // This is where you could integrate with your existing event infrastructure
            // or forward to external systems like Azure Event Hub, Service Bus, etc.
            
            ProcessTelemetryEvent(telemetryEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry notification: {Payload}", e.Payload);
        }
    }

    private void ProcessTelemetryEvent(JsonObject telemetryEvent)
    {
        // Placeholder for telemetry processing logic
        // In a production scenario, this could:
        // 1. Forward to time-series databases
        // 2. Send to message queues
        // 3. Trigger alerts or workflows
        // 4. Integrate with existing event sinks
        
        _logger.LogDebug("Processing telemetry event: {Event}", telemetryEvent.ToJsonString());
    }
}
