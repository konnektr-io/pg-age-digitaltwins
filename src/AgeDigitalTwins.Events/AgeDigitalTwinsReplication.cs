using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
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
    /// Maximum number of cloud events to batch together before sending to a sink.
    /// </summary>
    private const int MaxBatchSize = 50;

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
                        // Ensure replication slot exists, create if missing
                        await EnsureReplicationSlotExistsAsync(cancellationToken);

                        // Start the replication process
                        _logger.LogInformation(
                            "Starting replication on slot {ReplicationSlot}",
                            _replicationSlot
                        );
                        await StartReplicationAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        IsHealthy = false; // Mark as unhealthy on connection failure

                        // Check for connection-related errors that are common under high load
                        if (IsConnectionError(ex))
                        {
                            _logger.LogWarning(
                                ex,
                                "Connection error detected during replication: {Message}. This is common under high load. Retrying with backoff...",
                                ex.Message
                            );

                            // Dispose the current connection before retrying
                            if (_conn != null)
                            {
                                try
                                {
                                    await _conn.DisposeAsync();
                                }
                                catch (Exception disposeEx)
                                {
                                    _logger.LogDebug(
                                        disposeEx,
                                        "Error disposing connection during retry"
                                    );
                                }
                                finally
                                {
                                    _conn = null;
                                }
                            }

                            // Use exponential backoff for connection errors
                            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                        }
                        // Check if this is a replication slot invalidation error
                        else if (
                            ex.Message.Contains("can no longer get changes from replication slot")
                            || ex.Message.Contains("replication slot")
                                && ex.Message.Contains("invalidated")
                        )
                        {
                            _logger.LogError(
                                ex,
                                "Replication slot invalidation detected: {Message}. Attempting to recreate slot...",
                                ex.Message
                            );

                            try
                            {
                                await HandleInvalidatedSlotAsync(cancellationToken);
                                _logger.LogInformation(
                                    "Replication slot recreated successfully. Retrying immediately..."
                                );
                                continue; // Retry immediately after successful slot recreation
                            }
                            catch (Exception handleEx)
                            {
                                _logger.LogError(
                                    handleEx,
                                    "Failed to handle invalidated slot: {Message}. Will retry in 5 seconds...",
                                    handleEx.Message
                                );
                            }
                        }
                        else
                        {
                            _logger.LogError(
                                ex,
                                "Error during replication: {Message}\nRetrying in 5 seconds...",
                                ex.Message
                            );
                        }

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
        // Create connection with enhanced timeout settings for high load scenarios
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_connectionString)
        {
            Timeout = 0, // Disable command timeout for replication (replication is long-running)
            KeepAlive = 30, // TCP keepalive interval in seconds - this IS supported
            TcpKeepAlive = true, // Enable TCP keepalive - this IS supported
        };

        _conn = new LogicalReplicationConnection(connectionStringBuilder.ToString());
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
        DateTime lastMessageTime = DateTime.UtcNow;

        await foreach (PgOutputReplicationMessage message in messages)
        {
            try
            {
                lastMessageTime = DateTime.UtcNow; // Update last message timestamp

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

        var eventDataBatch = new List<EventData>();

        while (!cancellationToken.IsCancellationRequested)
        {
            // Try to dequeue events and batch them
            while (eventDataBatch.Count < MaxBatchSize && _eventQueue.TryDequeue(out var eventData))
            {
                if (eventData.EventType != null)
                {
                    eventDataBatch.Add(eventData);
                }
            }

            // Process the batch if we have events
            if (eventDataBatch.Count > 0)
            {
                await ProcessEventDataBatchAsync(eventDataBatch, eventSinks, eventRoutes);
                eventDataBatch.Clear();
            }
            else
            {
                // No events to process, wait before checking again
                await Task.Delay(100, cancellationToken);
            }
        }

        // Process any remaining events when cancellation is requested
        if (eventDataBatch.Count > 0)
        {
            await ProcessEventDataBatchAsync(eventDataBatch, eventSinks, eventRoutes);
        }
    }

    private async Task ProcessEventDataBatchAsync(
        List<EventData> eventDataBatch,
        List<IEventSink> eventSinks,
        List<EventRoute> eventRoutes
    )
    {
        // Group cloud events by sink for efficient batch sending
        var sinkEventGroups = new Dictionary<IEventSink, List<CloudEvent>>();

        foreach (var eventData in eventDataBatch)
        {
            foreach (EventRoute route in eventRoutes)
            {
                try
                {
                    _logger.LogDebug(
                        "Processing {EventType} with {EventFormat} from {Source} to sink {SinkName} \n{EventData}",
                        Enum.GetName(typeof(EventType), eventData.EventType!),
                        route.EventFormat,
                        _sourceUri,
                        route.SinkName,
                        JsonSerializer.Serialize(eventData, _jsonSerializerOptions)
                    );

                    var sink = eventSinks.FirstOrDefault(s => s.Name == route.SinkName);
                    if (sink == null)
                    {
                        continue;
                    }

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

                    // Add cloud events to the sink's batch
                    if (!sinkEventGroups.TryGetValue(sink, out List<CloudEvent>? value))
                    {
                        value = new List<CloudEvent>();
                        sinkEventGroups[sink] = value;
                    }

                    value.AddRange(cloudEvents);
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

        // Send all batched events to each sink
        var sendTasks = sinkEventGroups.Select(async kvp =>
        {
            var sink = kvp.Key;
            var events = kvp.Value;

            try
            {
                _logger.LogDebug(
                    "Sending batch of {EventCount} events to sink {SinkName}",
                    events.Count,
                    sink.Name
                );

                await sink.SendEventsAsync(events).ConfigureAwait(false);

                _logger.LogDebug(
                    "Successfully sent batch of {EventCount} events to sink {SinkName}",
                    events.Count,
                    sink.Name
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending batch of {EventCount} events to sink {SinkName}: {Message}",
                    events.Count,
                    sink.Name,
                    ex.Message
                );
            }
        });

        await Task.WhenAll(sendTasks);
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

    /// <summary>
    /// Ensures the replication slot exists and is valid, creating/recreating it if necessary.
    /// This handles cases where failover occurred, the slot doesn't exist, or the slot is invalidated.
    /// </summary>
    private async Task EnsureReplicationSlotExistsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var checkConn = new NpgsqlConnection(_connectionString);
            await checkConn.OpenAsync(cancellationToken);

            // Check if replication slot exists and get its status
            using var cmd = new NpgsqlCommand(
                @"SELECT slot_name, active, restart_lsn, confirmed_flush_lsn
                  FROM pg_replication_slots 
                  WHERE slot_name = @slotName",
                checkConn
            );
            cmd.Parameters.AddWithValue("slotName", _replicationSlot);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            bool slotExists = false;

            if (await reader.ReadAsync(cancellationToken))
            {
                slotExists = true;
                _logger.LogDebug("Replication slot {ReplicationSlot} exists", _replicationSlot);
            }

            await reader.CloseAsync();

            // If slot exists, we'll try to use it and let any invalidation errors be caught
            // by the retry logic in the calling method
            if (slotExists)
            {
                _logger.LogDebug(
                    "Replication slot {ReplicationSlot} exists, will attempt to use it",
                    _replicationSlot
                );
                return;
            }

            // Create the replication slot since it doesn't exist
            _logger.LogWarning(
                "Replication slot {ReplicationSlot} does not exist. Creating it now...",
                _replicationSlot
            );

            using var createCmd = new NpgsqlCommand(
                "SELECT * FROM pg_create_logical_replication_slot(@slotName, 'pgoutput')",
                checkConn
            );
            createCmd.Parameters.AddWithValue("slotName", _replicationSlot);

            await createCmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully created replication slot {ReplicationSlot}",
                _replicationSlot
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to ensure replication slot {ReplicationSlot} exists",
                _replicationSlot
            );
            throw;
        }
    }

    /// <summary>
    /// Handles invalidated replication slots by dropping and recreating them.
    /// This should be called when encountering slot invalidation errors.
    /// </summary>
    private async Task HandleInvalidatedSlotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning(
                "Handling invalidated replication slot {ReplicationSlot}. Dropping and recreating...",
                _replicationSlot
            );

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            // First, try to drop the slot
            try
            {
                using var dropCmd = new NpgsqlCommand(
                    "SELECT pg_drop_replication_slot(@slotName)",
                    conn
                );
                dropCmd.Parameters.AddWithValue("slotName", _replicationSlot);
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully dropped invalidated replication slot {ReplicationSlot}",
                    _replicationSlot
                );
            }
            catch (Exception dropEx)
            {
                // Slot might not exist anymore, log but continue
                _logger.LogWarning(
                    dropEx,
                    "Failed to drop replication slot {ReplicationSlot}, it may not exist: {Message}",
                    _replicationSlot,
                    dropEx.Message
                );
            }

            // Recreate the slot
            using var createCmd = new NpgsqlCommand(
                "SELECT * FROM pg_create_logical_replication_slot(@slotName, 'pgoutput')",
                conn
            );
            createCmd.Parameters.AddWithValue("slotName", _replicationSlot);
            await createCmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully recreated replication slot {ReplicationSlot}",
                _replicationSlot
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to handle invalidated replication slot {ReplicationSlot}",
                _replicationSlot
            );
            throw;
        }
    }

    /// <summary>
    /// Determines if an exception is a connection-related error that's commonly seen under high load.
    /// These errors typically indicate network issues, timeouts, or connection drops that can be retried.
    /// </summary>
    private static bool IsConnectionError(Exception ex)
    {
        // Check for common connection-related exceptions
        return ex is EndOfStreamException
            || ex is NpgsqlException npgsqlEx
                && (
                    npgsqlEx.Message.Contains("Exception while reading from stream")
                    || npgsqlEx.Message.Contains("Connection is not open")
                    || npgsqlEx.Message.Contains("The connection is broken")
                    || npgsqlEx.Message.Contains("timeout")
                    || npgsqlEx.Message.Contains("Connection terminated")
                    || npgsqlEx.Message.Contains("server closed the connection")
                    || npgsqlEx.InnerException is EndOfStreamException
                    || npgsqlEx.InnerException is SocketException
                    || npgsqlEx.InnerException is TimeoutException
                )
            || ex is SocketException
            || ex is TimeoutException
            || ex.Message.Contains("Exception while reading from stream")
            || ex.Message.Contains("Attempted to read past the end of the stream");
    }
}
