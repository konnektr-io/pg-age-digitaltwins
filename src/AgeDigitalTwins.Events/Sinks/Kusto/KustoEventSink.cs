using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Ingestion;
using Kusto.Ingest;

namespace AgeDigitalTwins.Events.Sinks.Kusto;

using AgeDigitalTwins.Events.Abstractions;
using AgeDigitalTwins.Events.Core.Events;



public class KustoEventSink : IEventSink, IDisposable
{
    private readonly KustoSinkOptions _options;
    private readonly IKustoQueuedIngestClient _ingestClient;
    private readonly ILogger _logger;
    private readonly Dictionary<string, KustoQueuedIngestionProperties> _ingestionProperties;
    private bool _isHealthy = true;

    public KustoEventSink(KustoSinkOptions options, TokenCredential credential, ILogger logger)
    {
        _options = options;
        _logger = logger;

        var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(
            _options.IngestionUri,
            _options.Database
        ).WithAadAzureTokenCredentialsAuthentication(credential);
        _ingestClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder);

        // Define the default ingestion properties for each event type
        var defaultIngestionProperties = new Dictionary<
            SinkEventType,
            KustoQueuedIngestionProperties
        >
        {
            {
                SinkEventType.PropertyEvent,
                new KustoQueuedIngestionProperties(
                    _options.Database,
                    _options.PropertyEventsTable ?? "AdtPropertyEvents"
                )
                {
                    ReportLevel = IngestionReportLevel.FailuresAndSuccesses,
                    ReportMethod = IngestionReportMethod.Table,
                    Format = DataSourceFormat.json,
                    IngestionMapping = new IngestionMapping
                    {
                        IngestionMappingKind = IngestionMappingKind.Json,
                        IngestionMappings =
                        [
                            new(
                                "TimeStamp",
                                "datetime",
                                new() { { MappingConsts.Path, "$.timeStamp" } }
                            ),
                            new(
                                "SourceTimeStamp",
                                "datetime",
                                new() { { MappingConsts.Path, "$.sourceTimeStamp" } }
                            ),
                            new(
                                "ServiceId",
                                "string",
                                new() { { MappingConsts.Path, "$.serviceId" } }
                            ),
                            new("Id", "string", new() { { MappingConsts.Path, "$.id" } }),
                            new("ModelId", "string", new() { { MappingConsts.Path, "$.modelId" } }),
                            new("Key", "string", new() { { MappingConsts.Path, "$.key" } }),
                            new("Value", "dynamic", new() { { MappingConsts.Path, "$.value" } }),
                            new(
                                "RelationshipTarget",
                                "string",
                                new() { { MappingConsts.Path, "$.relationshipTarget" } }
                            ),
                            new(
                                "RelationshipId",
                                "string",
                                new() { { MappingConsts.Path, "$.relationshipId" } }
                            ),
                            new("Action", "string", new() { { MappingConsts.Path, "$.action" } }),
                        ],
                    },
                }
            },
            {
                SinkEventType.TwinLifecycle,
                new KustoQueuedIngestionProperties(
                    _options.Database,
                    _options.TwinLifeCycleEventsTable ?? "AdtTwinLifeCycleEvents"
                )
                {
                    ReportLevel = IngestionReportLevel.FailuresAndSuccesses,
                    ReportMethod = IngestionReportMethod.Table,
                    Format = DataSourceFormat.json,
                    IngestionMapping = new IngestionMapping
                    {
                        IngestionMappingKind = IngestionMappingKind.Json,
                        IngestionMappings =
                        [
                            new(
                                "TimeStamp",
                                "datetime",
                                new() { { MappingConsts.Path, "$.timeStamp" } }
                            ),
                            new(
                                "ServiceId",
                                "string",
                                new() { { MappingConsts.Path, "$.serviceId" } }
                            ),
                            new("TwinId", "string", new() { { MappingConsts.Path, "$.twinId" } }),
                            new("Action", "string", new() { { MappingConsts.Path, "$.action" } }),
                            new("ModelId", "string", new() { { MappingConsts.Path, "$.modelId" } }),
                        ],
                    },
                }
            },
            {
                SinkEventType.RelationshipLifecycle,
                new KustoQueuedIngestionProperties(
                    _options.Database,
                    _options.RelationshipLifeCycleEventsTable ?? "AdtRelationshipLifeCycleEvents"
                )
                {
                    ReportLevel = IngestionReportLevel.FailuresAndSuccesses,
                    ReportMethod = IngestionReportMethod.Table,
                    Format = DataSourceFormat.json,
                    IngestionMapping = new IngestionMapping
                    {
                        IngestionMappingKind = IngestionMappingKind.Json,
                        IngestionMappings =
                        [
                            new(
                                "TimeStamp",
                                "datetime",
                                new() { { MappingConsts.Path, "$.timeStamp" } }
                            ),
                            new(
                                "ServiceId",
                                "string",
                                new() { { MappingConsts.Path, "$.serviceId" } }
                            ),
                            new(
                                "RelationshipId",
                                "string",
                                new() { { MappingConsts.Path, "$.relationshipId" } }
                            ),
                            new("Action", "string", new() { { MappingConsts.Path, "$.action" } }),
                            new("Name", "string", new() { { MappingConsts.Path, "$.name" } }),
                            new("Source", "string", new() { { MappingConsts.Path, "$.source" } }),
                            new("Target", "string", new() { { MappingConsts.Path, "$.target" } }),
                        ],
                    },
                }
            },
        };

        var mappings =
            _options.EventTypeMappings ?? CloudEventFactory.DefaultDataHistoryTypeMapping;

        // Build the _ingestionProperties dictionary using mapped event types if present
        _ingestionProperties = [];
        foreach (var mapping in mappings)
        {
            if (defaultIngestionProperties.TryGetValue(mapping.Key, out var prop))
            {
                _ingestionProperties[mapping.Value] = prop;
            }
        }
    }

    public string Name => _options.Name;

    /// <summary>
    /// Indicates whether the Kusto ingest client is healthy and able to send events.
    /// </summary>
    public bool IsHealthy => _isHealthy;

    public async Task SendEventsAsync(
        IEnumerable<CloudNative.CloudEvents.CloudEvent> cloudEvents,
        CancellationToken cancellationToken = default
    )
    {
        var eventsByType = cloudEvents.GroupBy(e => e.Type);

        foreach (var eventGroup in eventsByType)
        {
            try
            {
                var eventType = eventGroup.Key;
                if (eventType is null)
                {
                    _logger.LogWarning(
                        "Skipping event for sink '{SinkName}': Event type is null",
                        Name
                    );
                    continue;
                }

                if (!_ingestionProperties.TryGetValue(eventType, out var ingestionProperties))
                {
                    _logger.LogWarning(
                        "Skipping event for sink '{SinkName}': Unsupported event type: {EventType}",
                        Name,
                        eventType
                    );
                    continue;
                }

                using var stream = new MemoryStream();
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true))
                {
                    foreach (var cloudEvent in eventGroup)
                    {
                        if (cloudEvent.Data is not JsonObject data)
                        {
                            _logger.LogError(
                                "Skipping event for sink '{SinkName}': Data must be a JSON object",
                                Name
                            );
                            continue;
                        }

                        var jsonData = JsonSerializer.Serialize(data);
                        await writer.WriteLineAsync(jsonData);
                    }
                }
                stream.Position = 0;

                IKustoIngestionResult ingestionResult = await _ingestClient.IngestFromStreamAsync(
                    stream,
                    ingestionProperties
                );
                ingestionResult
                    .GetIngestionStatusCollection()
                    .ToList()
                    .ForEach(status =>
                    {
                        if (status.Status != Status.Pending && status.Status != Status.Succeeded)
                        {
                            _isHealthy = false;
                            _logger.LogError(
                                "Ingestion to Kusto sink '{SinkName}' failed: {Status}",
                                Name,
                                JsonSerializer.Serialize(status)
                            );
                        }
                        else
                        {
                            _isHealthy = true;
                            _logger.LogDebug(
                                "Ingestion to Kusto sink '{SinkName}' succeeded: {Status}",
                                Name,
                                JsonSerializer.Serialize(status)
                            );
                        }
                    });
                _logger.LogInformation(
                    "Ingested {EventCount} event(s) of type {EventType} with source {EventSource} to Kusto sink '{SinkName}'",
                    eventGroup.Count(),
                    eventType,
                    eventGroup.FirstOrDefault()?.Source?.ToString() ?? "unknown",
                    Name
                );
            }
            catch (Exception ex)
            {
                _isHealthy = false;
                _logger.LogError(
                    ex,
                    "Ingestion failed for sink {SinkName}: {Reason}",
                    Name,
                    ex.Message
                );
                // Optionally, you can rethrow the exception or handle it as needed.
                // For example, you might want to log it and continue processing other events.
            }
        }
    }

    public void Dispose()
    {
        _ingestClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

