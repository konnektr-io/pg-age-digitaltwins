using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using Npgsql;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Publishes telemetry data for a digital twin.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin.</param>
    /// <param name="telemetry">The telemetry data to publish.</param>
    /// <param name="messageId">Optional message ID for the telemetry. If not provided, a new GUID will be generated.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task PublishTelemetryAsync(
        string digitalTwinId,
        object telemetry,
        string? messageId = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "PublishTelemetryAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("messageId", messageId);

        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );

        await PublishTelemetryAsync(
            connection,
            digitalTwinId,
            telemetry,
            messageId,
            cancellationToken
        );
    }

    /// <summary>
    /// Publishes telemetry data for a specific component of a digital twin.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin.</param>
    /// <param name="componentName">The name of the component.</param>
    /// <param name="telemetry">The telemetry data to publish.</param>
    /// <param name="messageId">Optional message ID for the telemetry. If not provided, a new GUID will be generated.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task PublishComponentTelemetryAsync(
        string digitalTwinId,
        string componentName,
        object telemetry,
        string? messageId = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "PublishComponentTelemetryAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("componentName", componentName);
        activity?.SetTag("messageId", messageId);

        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );

        await PublishComponentTelemetryAsync(
            connection,
            digitalTwinId,
            componentName,
            telemetry,
            messageId,
            cancellationToken
        );
    }

    /// <summary>
    /// Internal method to publish telemetry data for a digital twin using an existing connection.
    /// </summary>
    internal async Task PublishTelemetryAsync(
        NpgsqlConnection connection,
        string digitalTwinId,
        object telemetry,
        string? messageId = null,
        CancellationToken cancellationToken = default
    )
    {
        messageId ??= Guid.NewGuid().ToString();
        DateTime timestamp = DateTime.UtcNow;

        // Try to get the model ID for DataSchema, but don't fail if it doesn't work (fire-and-forget)
        string? modelId = null;
        try
        {
            modelId = await GetModelIdByTwinIdCachedAsync(digitalTwinId, cancellationToken);
        }
        catch
        {
            // Silently ignore model ID lookup failures for fire-and-forget behavior
        }

        // Create the telemetry event payload
        var telemetryEvent = new JsonObject
        {
            ["digitalTwinId"] = digitalTwinId,
            ["messageId"] = messageId,
            ["timestamp"] = timestamp.ToString("o"),
            ["eventType"] = "Telemetry",
            ["telemetry"] = JsonSerializer.SerializeToNode(telemetry, serializerOptions),
        };

        // Add model ID if available
        if (!string.IsNullOrEmpty(modelId))
        {
            telemetryEvent["modelId"] = modelId;
        }

        // Publish via PostgreSQL NOTIFY
        string channel = "digitaltwins_telemetry";
        string payload = JsonSerializer.Serialize(telemetryEvent, serializerOptions);

        await using var command = new NpgsqlCommand("SELECT pg_notify($1, $2)", connection);
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(payload);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Internal method to publish component telemetry data using an existing connection.
    /// </summary>
    internal async Task PublishComponentTelemetryAsync(
        NpgsqlConnection connection,
        string digitalTwinId,
        string componentName,
        object telemetry,
        string? messageId = null,
        CancellationToken cancellationToken = default
    )
    {
        messageId ??= Guid.NewGuid().ToString();
        DateTime timestamp = DateTime.UtcNow;

        // Try to get the model ID for DataSchema, but don't fail if it doesn't work (fire-and-forget)
        string? modelId = null;
        try
        {
            modelId = await GetModelIdByTwinIdCachedAsync(digitalTwinId, cancellationToken);
        }
        catch
        {
            // Silently ignore model ID lookup failures for fire-and-forget behavior
        }

        // Create the component telemetry event payload
        var telemetryEvent = new JsonObject
        {
            ["digitalTwinId"] = digitalTwinId,
            ["componentName"] = componentName,
            ["messageId"] = messageId,
            ["timestamp"] = timestamp.ToString("o"),
            ["eventType"] = "ComponentTelemetry",
            ["telemetry"] = JsonSerializer.SerializeToNode(telemetry, serializerOptions),
        };

        // Add model ID if available
        if (!string.IsNullOrEmpty(modelId))
        {
            telemetryEvent["modelId"] = modelId;
        }

        // Publish via PostgreSQL NOTIFY
        string channel = "digitaltwins_telemetry";
        string payload = JsonSerializer.Serialize(telemetryEvent, serializerOptions);

        await using var command = new NpgsqlCommand("SELECT pg_notify($1, $2)", connection);
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(payload);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
