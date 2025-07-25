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
                    currentEvent = new EventData();
                    continue;
                }
                else if (message is InsertMessage insertMessage)
                {
                    if (currentEvent == null)
                    {
                        _logger.LogDebug("Skipping insert message without a transaction");
                        continue;
                    }

                    var newValue = await ConvertRowToJsonAsync(insertMessage.NewRow);
                    if (newValue == null)
                    {
                        _logger.LogDebug("Skipping insert message without a JSON value");
                        continue;
                    }

                    var newEntityId = GetEntityIdentifier(newValue);
                    var currentEntityId = GetCurrentEventEntityId(currentEvent);

                    _logger.LogDebug(
                        "Insert message - Current entity: {CurrentEntityId}, New entity: {NewEntityId}",
                        currentEntityId,
                        newEntityId
                    );

                    // Check if we're starting a new entity operation
                    if (
                        currentEntityId != null
                        && newEntityId != null
                        && currentEntityId != newEntityId
                    )
                    {
                        _logger.LogDebug(
                            "Entity transition detected, enqueueing current event for {CurrentEntityId} and starting new event for {NewEntityId}",
                            currentEntityId,
                            newEntityId
                        );
                        // Enqueue the current event and start a new one
                        EnqueueCurrentEventIfValid(currentEvent);
                        currentEvent = new EventData();
                    }

                    currentEvent.GraphName = insertMessage.Relation.Namespace;
                    currentEvent.TableName = insertMessage.Relation.RelationName;
                    currentEvent.OldValue = [];
                    currentEvent.NewValue = newValue;

                    if (newValue.ContainsKey("$dtId"))
                    {
                        currentEvent.EventType = EventType.TwinCreate;
                    }
                    else if (newValue.ContainsKey("$relationshipId"))
                    {
                        currentEvent.EventType = EventType.RelationshipCreate;
                    }
                    else
                    {
                        _logger.LogDebug("Skipping insert message without a valid JSON value");
                        continue;
                    }
                }
                else if (message is FullUpdateMessage updateMessage)
                {
                    if (currentEvent == null)
                    {
                        _logger.LogDebug("Skipping update message without a transaction");
                        continue;
                    }

                    var oldValue = await ConvertRowToJsonAsync(updateMessage.OldRow);
                    // In case oldValue is null, we should already have an oldValue in the current event
                    if (oldValue == null)
                    {
                        _logger.LogDebug("Skipping update message without an old JSON value");
                        continue;
                    }
                    var newValue = await ConvertRowToJsonAsync(updateMessage.NewRow);
                    if (newValue == null)
                    {
                        _logger.LogDebug("Skipping update message without a new JSON value");
                        continue;
                    }

                    var newEntityId = GetEntityIdentifier(newValue);
                    var currentEntityId = GetCurrentEventEntityId(currentEvent);

                    _logger.LogDebug(
                        "Update message - Current entity: {CurrentEntityId}, New entity: {NewEntityId}",
                        currentEntityId,
                        newEntityId
                    );

                    // Check if we're starting a new entity operation
                    if (
                        currentEntityId != null
                        && newEntityId != null
                        && currentEntityId != newEntityId
                    )
                    {
                        _logger.LogDebug(
                            "Entity transition detected in update, enqueueing current event for {CurrentEntityId} and starting new event for {NewEntityId}",
                            currentEntityId,
                            newEntityId
                        );
                        // Enqueue the current event and start a new one
                        EnqueueCurrentEventIfValid(currentEvent);
                        currentEvent = new EventData();
                    }

                    // Handle relationship MERGE/SET pattern logic
                    if (newValue.ContainsKey("$relationshipId"))
                    {
                        var eventWasEnqueued = HandleRelationshipUpdate(
                            currentEvent,
                            oldValue,
                            newValue,
                            updateMessage
                        );
                        if (eventWasEnqueued)
                        {
                            // Reset currentEvent since it was enqueued
                            currentEvent = new EventData();
                        }
                    }
                    else
                    {
                        // Regular twin update
                        currentEvent.GraphName = updateMessage.Relation.Namespace;
                        currentEvent.TableName = updateMessage.Relation.RelationName;
                        currentEvent.OldValue ??= oldValue;
                        currentEvent.NewValue = newValue;
                        currentEvent.EventType ??= EventType.TwinUpdate;
                    }
                }
                else if (message is FullDeleteMessage deleteMessage)
                {
                    if (currentEvent == null)
                    {
                        _logger.LogDebug("Skipping delete message without a transaction");
                        continue;
                    }

                    var oldValue = await ConvertRowToJsonAsync(deleteMessage.OldRow);
                    if (oldValue == null)
                    {
                        _logger.LogDebug("Skipping delete message without an old JSON value");
                        continue;
                    }

                    var deleteEntityId = GetEntityIdentifier(oldValue);
                    var currentEntityId = GetCurrentEventEntityId(currentEvent);

                    // Check if we're starting a new entity operation
                    if (
                        currentEntityId != null
                        && deleteEntityId != null
                        && currentEntityId != deleteEntityId
                    )
                    {
                        // Enqueue the current event and start a new one
                        EnqueueCurrentEventIfValid(currentEvent);
                        currentEvent = new EventData();
                    }

                    currentEvent.GraphName = deleteMessage.Relation.Namespace;
                    currentEvent.TableName = deleteMessage.Relation.RelationName;
                    currentEvent.OldValue = oldValue;

                    if (oldValue.ContainsKey("$dtId"))
                    {
                        currentEvent.EventType = EventType.TwinDelete;
                    }
                    else if (oldValue.ContainsKey("$relationshipId"))
                    {
                        currentEvent.EventType = EventType.RelationshipDelete;
                    }
                }
                else if (message is CommitMessage commitMessage)
                {
                    if (currentEvent == null)
                    {
                        _logger.LogDebug("Skipping commit message without a transaction");
                        continue;
                    }
                    currentEvent.Timestamp = commitMessage.TransactionCommitTimestamp;

                    // Enqueue the final event in this transaction
                    EnqueueCurrentEventIfValid(currentEvent);
                    currentEvent = null;

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

    public static async Task<JsonObject?> ConvertRowToJsonAsync(ReplicationTuple row)
    {
        try
        {
            await foreach (var value in row)
            {
                if (
                    value.GetFieldName() != "properties"
                    || value.GetDataTypeName() != "ag_catalog.agtype"
                )
                {
                    continue;
                }
                using Stream stream = value.GetStream();
                var bytes = new byte[stream.Length];
                await stream.ReadExactlyAsync(bytes.AsMemory(0, (int)stream.Length));
                var sValue = System.Text.Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<JsonObject>(sValue);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already been read"))
        {
            // Row has already been enumerated, return null and log the issue
            // This can happen when the same ReplicationTuple is processed multiple times
            return null;
        }
        return null;
    }

    /// <summary>
    /// Extracts a unique entity identifier from JSON data.
    /// For twins: returns $dtId
    /// For relationships: returns $relationshipId + $sourceId (concatenated)
    /// For models: returns id (future support)
    /// </summary>
    private static string? GetEntityIdentifier(JsonObject? jsonData)
    {
        if (jsonData == null)
            return null;

        // Twin identifier
        if (jsonData.ContainsKey("$dtId"))
        {
            return jsonData["$dtId"]?.ToString();
        }

        // Relationship identifier (combination of $relationshipId and $sourceId)
        if (jsonData.ContainsKey("$relationshipId") && jsonData.ContainsKey("$sourceId"))
        {
            var relationshipId = jsonData["$relationshipId"]?.ToString();
            var sourceId = jsonData["$sourceId"]?.ToString();
            return $"{sourceId}/{relationshipId}";
        }

        // Future: DTDL model identifier
        if (jsonData.ContainsKey("id"))
        {
            return jsonData["id"]?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gets the current entity identifier from an EventData object.
    /// Checks NewValue first, then OldValue.
    /// </summary>
    private static string? GetCurrentEventEntityId(EventData? eventData)
    {
        if (eventData == null)
            return null;

        return GetEntityIdentifier(eventData.NewValue) ?? GetEntityIdentifier(eventData.OldValue);
    }

    /// <summary>
    /// Handles relationship updates with MERGE/SET pattern detection.
    ///
    /// Scenarios:
    /// 1. New relationship creation:
    ///    - InsertMessage (incomplete) → FullUpdateMessage (incomplete → complete)
    /// 2. Existing relationship replacement:
    ///    - FullUpdateMessage (complete → incomplete) → FullUpdateMessage (incomplete → complete)
    ///    OR
    ///    - FullUpdateMessage (complete → complete) [direct update without MERGE/SET]
    /// </summary>
    /// <returns>True if an event was enqueued immediately, false otherwise</returns>
    private bool HandleRelationshipUpdate(
        EventData currentEvent,
        JsonObject oldValue,
        JsonObject newValue,
        FullUpdateMessage updateMessage
    )
    {
        var relationshipId = newValue["$relationshipId"]?.ToString();

        // Check if old and new values have complete relationship data
        var oldHasCompleteData = IsCompleteRelationshipData(oldValue);
        var newHasCompleteData = IsCompleteRelationshipData(newValue);

        _logger.LogDebug(
            "Relationship update - ID: {RelationshipId}, Old complete: {OldComplete}, New complete: {NewComplete}, CurrentEvent.OldValue complete: {CurrentOldComplete}",
            relationshipId,
            oldHasCompleteData,
            newHasCompleteData,
            IsCompleteRelationshipData(currentEvent.OldValue)
        );

        currentEvent.GraphName = updateMessage.Relation.Namespace;
        currentEvent.TableName = updateMessage.Relation.RelationName;

        if (!oldHasCompleteData && newHasCompleteData)
        {
            // Case: incomplete → complete (final step of MERGE/SET or creation)

            // Check if we already have a complete OldValue stored from a previous message
            // This would indicate an existing relationship replacement
            if (currentEvent.OldValue != null && IsCompleteRelationshipData(currentEvent.OldValue))
            {
                // Existing relationship replacement - we have the original complete old value
                currentEvent.NewValue = newValue;
                currentEvent.EventType = EventType.RelationshipUpdate;

                _logger.LogDebug(
                    "Detected existing relationship replacement for {RelationshipId} (using stored complete old value)",
                    relationshipId
                );
            }
            else
            {
                // New relationship creation - no prior complete old value
                currentEvent.OldValue = new JsonObject();
                currentEvent.NewValue = newValue;
                currentEvent.EventType = EventType.RelationshipCreate;

                _logger.LogDebug(
                    "Detected new relationship creation for {RelationshipId}",
                    relationshipId
                );
            }

            // Enqueue immediately since we have complete data
            _logger.LogDebug(
                "Immediately enqueueing relationship event for {RelationshipId}",
                relationshipId
            );
            EnqueueCurrentEventIfValid(currentEvent);
            return true; // Event was enqueued
        }
        else if (oldHasCompleteData && !newHasCompleteData)
        {
            // Case: complete → incomplete (first step of replacement MERGE)
            // Store the complete old value and wait for the next update
            currentEvent.OldValue = oldValue;
            currentEvent.NewValue = null; // Don't set yet, wait for complete data
            currentEvent.EventType = null; // Don't set yet, wait for complete data

            _logger.LogDebug(
                "Detected start of relationship replacement for {RelationshipId}, storing complete old value",
                relationshipId
            );
            return false; // No event enqueued yet
        }
        else if (oldHasCompleteData && newHasCompleteData)
        {
            // Case: complete → complete (direct update without MERGE/SET)
            // This can happen if the relationship is updated directly without going through MERGE/SET

            // Only treat this as an update if the values are actually different
            bool valuesAreDifferent = !JsonNode.DeepEquals(oldValue, newValue);

            if (valuesAreDifferent)
            {
                currentEvent.OldValue ??= oldValue;
                currentEvent.NewValue = newValue;
                currentEvent.EventType = EventType.RelationshipUpdate;

                _logger.LogDebug(
                    "Detected direct relationship update for {RelationshipId}",
                    relationshipId
                );
            }
            else
            {
                _logger.LogDebug(
                    "Ignoring relationship update for {RelationshipId} - values are identical",
                    relationshipId
                );
            }

            return false; // Will be enqueued later at commit
        }
        else
        {
            // Case: incomplete → incomplete (shouldn't happen often, but handle gracefully)
            _logger.LogWarning(
                "Unexpected incomplete → incomplete relationship update for {RelationshipId}",
                relationshipId
            );

            // Continue accumulating data, but don't overwrite a complete old value if we have one
            if (currentEvent.OldValue == null || !IsCompleteRelationshipData(currentEvent.OldValue))
            {
                currentEvent.OldValue = oldValue;
            }
            currentEvent.NewValue = newValue;
            return false; // No event enqueued yet
        }
    }

    /// <summary>
    /// Checks if a JsonObject contains complete relationship data.
    /// </summary>
    private static bool IsCompleteRelationshipData(JsonObject? jsonData)
    {
        if (jsonData == null)
            return false;

        return jsonData.ContainsKey("$sourceId")
            && jsonData.ContainsKey("$targetId")
            && jsonData.ContainsKey("$relationshipId")
            && jsonData["$sourceId"] != null
            && jsonData["$targetId"] != null
            && jsonData["$relationshipId"] != null;
    }

    /// <summary>
    /// Enqueues the current event if it has valid data and an event type.
    /// </summary>
    private void EnqueueCurrentEventIfValid(EventData? currentEvent)
    {
        if (currentEvent?.EventType != null && IsValidEventData(currentEvent))
        {
            var entityId = GetCurrentEventEntityId(currentEvent);
            _logger.LogDebug(
                "Enqueuing event: {EventType} for entity {EntityId}",
                currentEvent.EventType,
                entityId
            );
            _eventQueue.Enqueue(currentEvent);
            _logger.LogDebug(
                "Enqueued event: {Event}",
                JsonSerializer.Serialize(currentEvent, _jsonSerializerOptions)
            );
        }
        else
        {
            var entityId = GetCurrentEventEntityId(currentEvent);
            _logger.LogDebug(
                "Skipping enqueue for invalid event - Type: {EventType}, EntityId: {EntityId}",
                currentEvent?.EventType,
                entityId
            );
        }
    }

    /// <summary>
    /// Validates that an EventData object has the minimum required data structure.
    /// </summary>
    private static bool IsValidEventData(EventData eventData)
    {
        if (eventData.NewValue == null)
            return false;

        // For twin events, must have $dtId
        if (
            eventData.EventType
            is EventType.TwinCreate
                or EventType.TwinUpdate
                or EventType.TwinDelete
        )
        {
            return eventData.NewValue.ContainsKey("$dtId")
                && !string.IsNullOrEmpty(eventData.NewValue["$dtId"]?.ToString());
        }

        // For relationship events, must have complete relationship data
        if (
            eventData.EventType
            is EventType.RelationshipCreate
                or EventType.RelationshipUpdate
                or EventType.RelationshipDelete
        )
        {
            return IsCompleteRelationshipData(eventData.NewValue);
        }

        return false;
    }
}
