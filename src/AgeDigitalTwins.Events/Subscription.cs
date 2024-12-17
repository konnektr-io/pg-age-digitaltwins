using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudNative.CloudEvents;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace AgeDigitalTwins.Events;

public class AgeDigitalTwinsSubscription : IAsyncDisposable
{
    public AgeDigitalTwinsSubscription(
        string connectionString,
        string publication,
        string replicationSlot,
        EventSinkFactory eventSinkFactory,
        ILogger<AgeDigitalTwinsSubscription> logger
    )
    {
        _connectionString = connectionString;
        _publication = publication;
        _replicationSlot = replicationSlot;
        _eventSinkFactory = eventSinkFactory;
        _logger = logger;
        NpgsqlConnectionStringBuilder csb = new(connectionString);
        _serverUri = new Uri($"{csb.Host}:{csb.Port}", UriKind.Absolute);
    }

    private readonly string _connectionString;
    private readonly string _publication;
    private readonly string _replicationSlot;
    private readonly Uri _serverUri;
    private readonly EventSinkFactory _eventSinkFactory;
    private readonly ILogger<AgeDigitalTwinsSubscription> _logger;
    private LogicalReplicationConnection? _conn;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentQueue<EventData> _eventQueue = new();

    public async Task StartAsync()
    {
        var consumerTask = Task.Run(ConsumeQueueAsync);
        while (true)
        {
            try
            {
                await StartReplicationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Error during replication: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5)); // Wait before retrying
            }
        }
    }

    private async Task StartReplicationAsync()
    {
        _conn = new LogicalReplicationConnection(_connectionString);
        await _conn.Open();

        PgOutputReplicationSlot slot = new(_replicationSlot);
        _cancellationTokenSource = new CancellationTokenSource();

        IAsyncEnumerable<PgOutputReplicationMessage> messages = _conn.StartReplication(
            slot,
            new PgOutputReplicationOptions(_publication, PgOutputProtocolVersion.V4),
            _cancellationTokenSource.Token
        );

        EventData? currentEvent = null;

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
                    currentEvent.GraphName = insertMessage.Relation.Namespace;
                    currentEvent.TableName = insertMessage.Relation.RelationName;
                    currentEvent.NewValue = await ConvertRowToJsonAsync(insertMessage.NewRow);
                    if (currentEvent.NewValue != null)
                    {
                        if (currentEvent.NewValue.ContainsKey("$dtId"))
                        {
                            currentEvent.EventType = EventType.TwinCreate;
                        }
                        else if (currentEvent.NewValue.ContainsKey("$relationshipId"))
                        {
                            currentEvent.EventType = EventType.RelationshipCreate;
                        }
                        else
                        {
                            _logger.LogDebug("Skipping insert message without a valid JSON value");
                            continue;
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Skipping insert message without a JSON value");
                        continue;
                    }
                    // Console.WriteLine($"Inserted row: {currentEvent.NewValue}");
                }
                else if (message is FullUpdateMessage updateMessage)
                {
                    if (currentEvent == null)
                    {
                        _logger.LogDebug("Skipping update message without a transaction");
                        continue;
                    }
                    currentEvent.GraphName = updateMessage.Relation.Namespace;
                    currentEvent.TableName = updateMessage.Relation.RelationName;
                    currentEvent.OldValue ??= await ConvertRowToJsonAsync(updateMessage.OldRow);
                    currentEvent.NewValue = await ConvertRowToJsonAsync(updateMessage.NewRow);
                    if (currentEvent.EventType == null && currentEvent.NewValue != null)
                    {
                        if (currentEvent.NewValue.ContainsKey("$dtId"))
                        {
                            currentEvent.EventType = EventType.TwinUpdate;
                        }
                        else if (currentEvent.NewValue.ContainsKey("$relationshipId"))
                        {
                            currentEvent.EventType = EventType.RelationshipUpdate;
                        }
                    }
                    // Console.WriteLine($"Updated row: {currentEvent.OldValue} -> {currentEvent.NewValue}");
                }
                else if (message is FullDeleteMessage deleteMessage)
                {
                    if (currentEvent == null)
                    {
                        _logger.LogDebug("Skipping delete message without a transaction");
                        continue;
                    }
                    currentEvent.GraphName = deleteMessage.Relation.Namespace;
                    currentEvent.TableName = deleteMessage.Relation.RelationName;
                    currentEvent.OldValue = await ConvertRowToJsonAsync(deleteMessage.OldRow);
                    if (currentEvent.OldValue != null)
                    {
                        if (currentEvent.OldValue.ContainsKey("$dtId"))
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
                    if (currentEvent == null)
                    {
                        _logger.LogDebug("Skipping commit message without a transaction");
                        continue;
                    }
                    currentEvent.Timestamp = commitMessage.TransactionCommitTimestamp;
                    _eventQueue.Enqueue(currentEvent);
                    currentEvent = null;
                }
                // Implement fallbacks in case replica identity is not set to full
                // This means we might need to fetch the value from the database
                // This also won't work for updates, because we need the old value
                else
                {
                    // _logger.LogInformation($"Skipping message type: {message.GetType().Name}");
                }

                // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
                // so that Npgsql can inform the server which WAL files can be removed/recycled.
                _conn.SetReplicationStatus(message.WalEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Message}", ex.Message);
            }
        }
    }

    private async Task ConsumeQueueAsync()
    {
        List<IEventSink> eventSinks = _eventSinkFactory.CreateEventSinks();
        List<EventRoute> eventRoutes = _eventSinkFactory.GetEventRoutes();

        while (true)
        {
            if (_eventQueue.TryDequeue(out var eventData) && eventData.EventType != null)
            {
                foreach (EventRoute route in eventRoutes)
                {
                    try
                    {
                        _logger.LogDebug(
                            "Processing {EventType} from {_serverUri} for event route: {Route}\n{EventData}",
                            Enum.GetName(typeof(EventType), eventData.EventType),
                            _serverUri,
                            JsonSerializer.Serialize(route),
                            JsonSerializer.Serialize(eventData)
                        );
                        if (
                            route.EventTypes == null
                            || route.EventTypes.Contains((EventType)eventData.EventType)
                        )
                        {
                            var sink = eventSinks.FirstOrDefault(s => s.Name == route.SinkName);
                            if (sink != null)
                            {
                                List<CloudEvent> cloudEvents;
                                if (route.EventFormat == EventFormat.EventNotification)
                                {
                                    cloudEvents = CloudEventFactory.CreateEventNotificationEvents(
                                        eventData,
                                        _serverUri
                                    );
                                }
                                else if (route.EventFormat == EventFormat.DataHistory)
                                {
                                    cloudEvents = CloudEventFactory.CreateDataHistoryEvents(
                                        eventData,
                                        _serverUri
                                    );
                                }
                                else
                                {
                                    _logger.LogDebug(
                                        "Skipping event route without a valid event format"
                                    );
                                    continue;
                                }
                                if (cloudEvents.Count == 0)
                                {
                                    _logger.LogDebug("Skipping event route without any events");
                                    continue;
                                }
                                await sink.SendEventsAsync(cloudEvents);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error while processing event route {Route}: {Message}",
                            route,
                            ex.Message
                        );
                    }
                }
            }
            else
            {
                await Task.Delay(100); // Wait before checking the queue again
            }
        }
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_conn != null)
        {
            await _conn.DisposeAsync();
            _conn = null;
        }
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    public static async Task<JsonObject?> ConvertRowToJsonAsync(ReplicationTuple row)
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
        return null;
    }
}
