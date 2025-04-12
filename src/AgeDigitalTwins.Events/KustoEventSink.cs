using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Ingestion;
using Kusto.Ingest;

namespace AgeDigitalTwins.Events;

public class KustoEventSink : IEventSink, IDisposable
{
    private readonly KustoSinkOptions _options;
    private readonly IKustoQueuedIngestClient _ingestClient;
    private readonly ILogger _logger;
    private readonly Dictionary<string, KustoQueuedIngestionProperties> _ingestionProperties;

    public KustoEventSink(KustoSinkOptions options, TokenCredential credential, ILogger logger)
    {
        _options = options;
        _logger = logger;

        var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(
            _options.IngestionUri
        ).WithAadAzureTokenCredentialsAuthentication(credential);
        _ingestClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder);

        _ingestionProperties = new Dictionary<string, KustoQueuedIngestionProperties>
        {
            {
                "DigitalTwin.Property.Event",
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
                "DigitalTwin.Twin.Lifecycle",
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
                "DigitalTwin.Relationship.Lifecycle",
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
    }

    public string Name => _options.Name;

    public async Task SendEventsAsync(IEnumerable<CloudNative.CloudEvents.CloudEvent> cloudEvents)
    {
        var eventsByType = cloudEvents.GroupBy(e => e.Type);

        foreach (var eventGroup in eventsByType)
        {
            try
            {
                var eventType = eventGroup.Key;
                if (eventType is null)
                {
                    _logger.LogWarning("Event type must be specified");
                    continue;
                }

                if (!_ingestionProperties.TryGetValue(eventType, out var ingestionProperties))
                {
                    _logger.LogWarning("Unsupported event type: {EventType}", eventType);
                    continue;
                }

                using var stream = new MemoryStream();
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true))
                {
                    foreach (var cloudEvent in eventGroup)
                    {
                        if (cloudEvent.Data is not JsonObject data)
                        {
                            _logger.LogError("Data must be a JSON object");
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
                            _logger.LogError(
                                "Ingestion to Kusto failed: {Status}",
                                JsonSerializer.Serialize(status)
                            );
                        }
                    });
                _logger.LogDebug(
                    "Ingested {EventCount} events of type {EventType} to Kusto",
                    eventGroup.Count(),
                    eventType
                );
            }
            catch (Exception ex)
            {
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

public class KustoSinkOptions
{
    public required string Name { get; set; }
    public required string IngestionUri { get; set; }
    public required string Database { get; set; }
    public string? PropertyEventsTable { get; set; }
    public string? TwinLifeCycleEventsTable { get; set; }
    public string? RelationshipLifeCycleEventsTable { get; set; }
}
