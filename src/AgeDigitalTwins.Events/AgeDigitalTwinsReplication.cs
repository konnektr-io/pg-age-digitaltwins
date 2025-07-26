using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CloudNative.CloudEvents;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace AgeDigitalTwins.Events;

public class AgeDigitalTwinsReplication : IAsyncDisposable
{
    private static readonly ActivitySource ActivitySource = new("AgeDigitalTwins.Events", "1.0.0");

    public AgeDigitalTwinsReplication(
        string connectionString,
        string publication,
        string replicationSlot,
        string? source,
        EventSinkFactory eventSinkFactory,
        ILogger<AgeDigitalTwinsReplication> logger
    )
    {
        _connectionString = connectionString;
        _publication = publication;
        _replicationSlot = replicationSlot;
        _eventSinkFactory = eventSinkFactory;
        _logger = logger;

        if (!string.IsNullOrEmpty(source))
        {
            if (!Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out _sourceUri!))
            {
                UriBuilder uriBuilder = new(source);
                _sourceUri = uriBuilder.Uri;
            }
        }
        else
        {
            NpgsqlConnectionStringBuilder csb = new(connectionString);
            UriBuilder uriBuilder = new() { Scheme = "postgresql", Host = csb.Host };
            _sourceUri = uriBuilder.Uri;
        }
    }

    /// <summary>
    /// The connection string to the PostgreSQL database. This is used to connect to the database.
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// The publication name. This is used to identify the publication in PostgreSQL.
    /// Defaults to "age_pub".
    /// </summary>
    private readonly string _publication;

    /// <summary>
    /// The replication slot name. This is used to identify the replication slot in PostgreSQL.
    /// Defaults to "age_slot".
    /// </summary>
    private readonly string _replicationSlot;

    /// <summary>
    /// The source URI for the event sink. This is used to identify the source of the events.
    /// </summary>
    private readonly Uri _sourceUri;

    /// <summary>
    /// Factory for creating event sinks. This is used to create the sinks that will process the events.
    /// </summary>
    private readonly EventSinkFactory _eventSinkFactory;
    private readonly ILogger<AgeDigitalTwinsReplication> _logger;
    private LogicalReplicationConnection? _conn;
    private readonly ConcurrentQueue<EventData> _eventQueue = new();

    /// <summary>
    /// Indicates whether the replication connection is currently healthy.
    /// Used by health checks to determine service status.
    /// </summary>
    public bool IsHealthy { get; private set; } = false;

    private readonly JsonSerializerOptions _jsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var replicationTask = Task.Run(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await StartReplicationAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        IsHealthy = false; // Mark as unhealthy on connection failure
                        _logger.LogError(
                            ex,
                            "Error during replication: {Message}\nRetrying in 5 seconds...",
                            ex.Message
                        );
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before retrying
                    }
                }
            },
            cancellationToken
        );

        var consumerTask = Task.Run(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await ConsumeQueueAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error while consuming the event queue: {Message}\nRetrying in 5 seconds...",
                            ex.Message
                        );
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before retrying
                    }
                }
            },
            cancellationToken
        );

        await Task.WhenAll(replicationTask, consumerTask);
    }

    private async Task StartReplicationAsync(CancellationToken cancellationToken = default)
    {
        _conn = new LogicalReplicationConnection(_connectionString);
        await _conn.Open(cancellationToken);

        PgOutputReplicationSlot slot = new(_replicationSlot);

        IAsyncEnumerable<PgOutputReplicationMessage> messages = _conn.StartReplication(
            slot,
            new PgOutputReplicationOptions(_publication, PgOutputProtocolVersion.V4),
            cancellationToken
        );

        _logger.LogInformation(
            "Started replication on slot {ReplicationSlot} for publication {Publication}",
            _replicationSlot,
            _publication
        );

        // Mark as healthy now that replication has started successfully
        IsHealthy = true;

        EventData? currentEvent = null;
        Activity? transactionActivity = null;

        await foreach (PgOutputReplicationMessage message in messages)
        {
            try
            {
                _logger.LogDebug(
                    "Received message type: {ReplicationMessageType}",
                    message.GetType().Name
                );

                if (message is BeginMessage beginMessage)
                {
                    transactionActivity = ActivitySource.StartActivity(
                        "Transaction",
                        ActivityKind.Consumer
                    );
                    transactionActivity?.SetTag("transaction.xid", beginMessage.TransactionXid);
                    continue;
                }
                else if (message is InsertMessage insertMessage)
                {
                    if (insertMessage.Relation.Namespace == "ag_catalog")
                    {
                        // Skip messages from ag_catalog relations
                        continue;
                    }

                    var (id, newValue) = await ConvertRowToJsonAsync(insertMessage.NewRow);

                    if (id == null)
                    {
                        _logger.LogInformation(
                            "Skipping insert message without a valid id - Id: {NewEntityId}, Value: {NewValue}",
                            id,
                            newValue
                        );
                        continue;
                    }

                    _logger.LogDebug(
                        "Insert message - Id: {NewEntityId}, Value: {NewValue}",
                        id,
                        newValue
                    );

                    if (
                        currentEvent != null
                        && (
                            currentEvent.Id != id
                            || currentEvent.TableName != insertMessage.Relation.RelationName
                        )
                    )
                    {
                        _logger.LogDebug(
                            "Entity transition detected in insert, enqueueing current event for {CurrentEntityId} and starting new event for {NewEntityId}",
                            currentEvent.Id,
                            id
                        );
                        // Enqueue the current event and start a new one
                        EnqueueCurrentEventIfValid(currentEvent);
                    }

                    // Start a new event for the insert
                    currentEvent = new EventData(
                        id: id,
                        graphName: insertMessage.Relation.Namespace,
                        tableName: insertMessage.Relation.RelationName,
                        timestamp: insertMessage.ServerClock
                    )
                    {
                        OldValue = [],
                        NewValue = newValue,
                    };

                    if (newValue?.ContainsKey("$dtId") == true || currentEvent.TableName == "Twin")
                    {
                        currentEvent.EventType = EventType.TwinCreate;
                    }
                    else if (newValue?.ContainsKey("$relationshipId") == true)
                    {
                        currentEvent.EventType = EventType.RelationshipCreate;
                    }
                }
                else if (message is FullUpdateMessage updateMessage)
                {
                    if (updateMessage.Relation.Namespace == "ag_catalog")
                    {
                        // Skip messages from ag_catalog relations
                        continue;
                    }

                    var (oldId, oldValue) = await ConvertRowToJsonAsync(updateMessage.OldRow);
                    var (newId, newValue) = await ConvertRowToJsonAsync(updateMessage.NewRow);

                    if (oldId == null || newId == null || !string.Equals(oldId, newId))
                    {
                        _logger.LogInformation(
                            "Skipping update message without valid IDs - Old ID: {OldEntityId}, New ID: {NewEntityId}, Old Value: {OldValue}, New Value: {NewValue}",
                            oldId,
                            newId,
                            oldValue,
                            newValue
                        );
                        continue;
                    }

                    _logger.LogDebug(
                        "Update message - OldId: {OldEntityId}, OldValue: {OldValue}, NewId: {NewEntityId}, NewValue: {NewValue}",
                        oldId,
                        newId,
                        oldValue,
                        newValue
                    );

                    // Check if we're starting a new entity operation (and enqueue current event if needed)
                    if (
                        currentEvent != null
                        && (
                            currentEvent.Id != newId
                            || currentEvent.TableName != updateMessage.Relation.RelationName
                        )
                    )
                    {
                        _logger.LogDebug(
                            "Entity transition detected in update, enqueueing current event for {CurrentEntityId} and starting new event for {NewEntityId}",
                            currentEvent.Id,
                            newId
                        );
                        // Enqueue the current event and start a new one
                        EnqueueCurrentEventIfValid(currentEvent);
                        currentEvent = null;
                    }

                    // If currentEvent is null, we need to create a new one
                    currentEvent ??= new EventData(
                        id: newId,
                        graphName: updateMessage.Relation.Namespace,
                        tableName: updateMessage.Relation.RelationName,
                        timestamp: updateMessage.ServerClock
                    );

                    currentEvent.OldValue ??= oldValue;
                    currentEvent.NewValue = newValue;

                    if (currentEvent.EventType == null && currentEvent.OldValue != null)
                    {
                        if (
                            newValue?.ContainsKey("$dtId") == true
                            || currentEvent.OldValue.ContainsKey("$dtId")
                            || currentEvent.TableName == "Twin"
                        )
                        {
                            currentEvent.EventType = EventType.TwinUpdate;
                        }
                        else if (
                            newValue?.ContainsKey("$relationshipId") == true
                            || currentEvent.OldValue.ContainsKey("$relationshipId")
                        )
                        {
                            currentEvent.EventType = EventType.RelationshipUpdate;
                        }
                    }
                }
                else if (message is FullDeleteMessage deleteMessage)
                {
                    if (deleteMessage.Relation.Namespace == "ag_catalog")
                    {
                        // Skip messages from ag_catalog relations
                        continue;
                    }

                    var (oldId, oldValue) = await ConvertRowToJsonAsync(deleteMessage.OldRow);
                    if (oldId == null)
                    {
                        _logger.LogInformation(
                            "Skipping delete message without valid ID - Old ID: {OldEntityId}, Old Value: {OldValue}",
                            oldId,
                            oldValue
                        );
                        continue;
                    }

                    // Check if we're starting a new entity operation (and enqueue current event if needed)
                    if (
                        currentEvent != null
                        && (
                            currentEvent.Id != oldId
                            || currentEvent.TableName != deleteMessage.Relation.RelationName
                        )
                    )
                    {
                        _logger.LogDebug(
                            "Entity transition detected in delete, enqueueing current event for {CurrentEntityId} and starting new event for {OldEntityId}",
                            currentEvent.Id,
                            oldId
                        );
                        // Enqueue the current event and start a new one
                        EnqueueCurrentEventIfValid(currentEvent);
                        currentEvent = null;
                    }

                    // If currentEvent is null, we need to create a new one
                    currentEvent ??= new EventData(
                        id: oldId,
                        graphName: deleteMessage.Relation.Namespace,
                        tableName: deleteMessage.Relation.RelationName,
                        timestamp: deleteMessage.ServerClock
                    );

                    currentEvent.OldValue ??= oldValue;

                    if (currentEvent.EventType == null && currentEvent.OldValue != null)
                    {
                        if (
                            currentEvent.OldValue.ContainsKey("$dtId")
                            || currentEvent.TableName == "Twin"
                        )
                        {
                            currentEvent.EventType = EventType.TwinDelete;
                        }
                        else if (currentEvent.OldValue.ContainsKey("$relationshipId"))
                        {
                            currentEvent.EventType = EventType.RelationshipDelete;
                        }
                    }
                }
                else if (message is CommitMessage commitMessage)
                {
                    if (currentEvent != null)
                    {
                        _logger.LogDebug(
                            "Transaction commited, enqueueing current event for {CurrentEntityId}",
                            currentEvent.Id
                        );

                        // Enqueue the final event in this transaction
                        EnqueueCurrentEventIfValid(currentEvent);
                        currentEvent = null;
                    }

                    transactionActivity?.Stop();
                    transactionActivity?.Dispose();
                    transactionActivity = null;
                }
                else
                {
                    // In case replica identity is not correctly set, or when messages from user defined tables are received
                    _logger.LogDebug(
                        "Skipping message type: {MessageType}",
                        message.GetType().Name
                    );
                }

                // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
                // so that Npgsql can inform the server which WAL files can be removed/recycled.
                _conn.SetReplicationStatus(message.WalEnd);
            }
            catch (Exception ex)
            {
                transactionActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                transactionActivity?.AddEvent(
                    new ActivityEvent(
                        "Exception",
                        default,
                        new ActivityTagsCollection
                        {
                            { "exception.type", ex.GetType().FullName },
                            { "exception.message", ex.Message },
                            { "exception.stacktrace", ex.StackTrace },
                        }
                    )
                );

                transactionActivity?.Stop();
                transactionActivity?.Dispose();
                transactionActivity = null;

                _logger.LogError(ex, "Error processing message: {Message}", ex.Message);
            }
        }
    }

    private async Task ConsumeQueueAsync(CancellationToken cancellationToken = default)
    {
        List<IEventSink> eventSinks = _eventSinkFactory.CreateEventSinks();
        _logger.LogInformation(
            "Event sinks created: {Sinks}",
            string.Join(',', eventSinks.Select(r => r.Name))
        );
        if (eventSinks.Count == 0)
        {
            _logger.LogWarning("No event sinks configured. Exiting.");
            return;
        }
        List<EventRoute> eventRoutes = _eventSinkFactory.GetEventRoutes();
        _logger.LogInformation(
            "Event routes created: {Routes}",
            JsonSerializer.Serialize(eventRoutes, _jsonSerializerOptions)
        );
        if (eventRoutes.Count == 0)
        {
            _logger.LogWarning("No event routes configured. Exiting.");
            return;
        }

        while (true)
        {
            if (_eventQueue.TryDequeue(out var eventData) && eventData.EventType != null)
            {
                foreach (EventRoute route in eventRoutes)
                {
                    try
                    {
                        _logger.LogDebug(
                            "Processing {EventType} with {EventFormat} from {Source} to sink {SinkName} \n{EventData}",
                            Enum.GetName(typeof(EventType), eventData.EventType),
                            route.EventFormat,
                            _sourceUri,
                            route.SinkName,
                            JsonSerializer.Serialize(eventData, _jsonSerializerOptions)
                        );
                        // Removed EventTypes filter: always process event for this route
                        var sink = eventSinks.FirstOrDefault(s => s.Name == route.SinkName);
                        if (sink != null)
                        {
                            List<CloudEvent> cloudEvents;
                            // Get the typeMapping from the sink's EventTypeMappings property
                            var sinkOptions = sink as SinkOptions;
                            var typeMapping = sinkOptions?.EventTypeMappings;
                            if (route.EventFormat == EventFormat.EventNotification)
                            {
                                cloudEvents = CloudEventFactory.CreateEventNotificationEvents(
                                    eventData,
                                    _sourceUri,
                                    typeMapping
                                );
                            }
                            else if (route.EventFormat == EventFormat.DataHistory)
                            {
                                cloudEvents = CloudEventFactory.CreateDataHistoryEvents(
                                    eventData,
                                    _sourceUri,
                                    typeMapping
                                );
                            }
                            else
                            {
                                _logger.LogDebug(
                                    "Skipping event route for {SinkName} with unsupported event format {EventFormat}",
                                    route.SinkName,
                                    route.EventFormat
                                );
                                continue;
                            }
                            if (cloudEvents.Count == 0)
                            {
                                _logger.LogDebug(
                                    "Skipping event route for {SinkName} without any events",
                                    route.SinkName
                                );
                                continue;
                            }
                            await sink.SendEventsAsync(cloudEvents).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error while processing event route {Route}: {Message}",
                            JsonSerializer.Serialize(route, _jsonSerializerOptions),
                            ex.Message
                        );
                    }
                }
            }
            else
            {
                await Task.Delay(100, cancellationToken); // Wait before checking the queue again
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        IsHealthy = false; // Mark as unhealthy when disposing
        if (_conn != null)
        {
            await _conn.DisposeAsync();
            _conn = null;
        }
        GC.SuppressFinalize(this);
    }

    public static async Task<(string?, JsonObject?)> ConvertRowToJsonAsync(ReplicationTuple row)
    {
        string? id = null;
        JsonObject? properties = null;
        await foreach (var value in row)
        {
            if (value.GetFieldName() == "id" && value.GetDataTypeName() == "ag_catalog.graphid")
            {
                using Stream stream = value.GetStream();
                var bytes = new byte[stream.Length];
                await stream.ReadExactlyAsync(bytes.AsMemory(0, (int)stream.Length));
                id = System.Text.Encoding.UTF8.GetString(bytes);
            }
            if (
                value.GetFieldName() == "properties"
                && value.GetDataTypeName() == "ag_catalog.agtype"
            )
            {
                using Stream stream = value.GetStream();
                var bytes = new byte[stream.Length];
                await stream.ReadExactlyAsync(bytes.AsMemory(0, (int)stream.Length));
                var sValue = System.Text.Encoding.UTF8.GetString(bytes);
                properties = JsonSerializer.Deserialize<JsonObject>(sValue);
            }
        }
        return (id, properties);
    }

    /// <summary>
    /// Enqueues the current event if it has valid data and an event type.
    /// </summary>
    private void EnqueueCurrentEventIfValid(EventData? currentEvent)
    {
        if (currentEvent?.EventType != null && IsValidEventData(currentEvent))
        {
            _logger.LogDebug(
                "Enqueuing event: Type: {EventType}, EntityId: {EntityId}, TableName: {TableName}, GraphName: {GraphName}",
                currentEvent.EventType,
                currentEvent.Id,
                currentEvent.TableName,
                currentEvent.GraphName
            );
            _eventQueue.Enqueue(currentEvent);
        }
        else
        {
            _logger.LogWarning(
                "Skipping enqueue for invalid event - Type: {EventType}, EntityId: {EntityId}, TableName: {TableName}, GraphName: {GraphName}",
                currentEvent?.EventType,
                currentEvent?.Id,
                currentEvent?.TableName,
                currentEvent?.GraphName
            );
        }
    }

    /// <summary>
    /// Validates that an EventData object has the minimum required data structure.
    /// </summary>
    private static bool IsValidEventData(EventData eventData)
    {
        // For Create or Update, NewValue must be present
        if (
            eventData.EventType
            is EventType.TwinCreate
                or EventType.TwinUpdate
                or EventType.RelationshipCreate
                or EventType.RelationshipUpdate
        )
        {
            if (eventData.NewValue == null)
            {
                return false;
            }
        }

        // For Update, OldValue must be present
        if (eventData.EventType is EventType.TwinUpdate or EventType.RelationshipUpdate)
        {
            if (eventData.OldValue == null)
            {
                return false;
            }
        }

        return true;
    }
}
