using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudNative.CloudEvents;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

namespace AgeDigitalTwins.Events;

public class AgeDigitalTwinsSubscription(
    string connectionString,
    string publication,
    string replicationSlot,
    EventSinkFactory eventSinkFactory
) : IAsyncDisposable
{
    private readonly string _connectionString = connectionString;
    private readonly string _publication = publication;
    private readonly string _replicationSlot = replicationSlot;
    private readonly EventSinkFactory _eventSinkFactory = eventSinkFactory;
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
                Console.WriteLine($"Error during replication: {ex.Message}");
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
                Console.WriteLine($"Received message type: {message.GetType().Name}");

                if (message is BeginMessage beginMessage)
                {
                    // Console.WriteLine("Begin transaction");
                    currentEvent = new EventData();
                    continue;
                }
                else if (message is InsertMessage insertMessage)
                {
                    if (currentEvent == null)
                    {
                        Console.WriteLine("Skipping insert message without a transaction");
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
                            Console.WriteLine("Skipping insert message without a valid JSON value");
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Skipping insert message without a JSON value");
                        continue;
                    }
                    // Console.WriteLine($"Inserted row: {currentEvent.NewValue}");
                }
                else if (message is FullUpdateMessage updateMessage)
                {
                    if (currentEvent == null)
                    {
                        Console.WriteLine("Skipping update message without a transaction");
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
                        Console.WriteLine("Skipping delete message without a transaction");
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
                else if (message is CommitMessage)
                {
                    if (currentEvent == null)
                    {
                        Console.WriteLine("Skipping commit message without a transaction");
                        continue;
                    }
                    // Console.WriteLine("Commit transaction");
                    _eventQueue.Enqueue(currentEvent);
                    currentEvent = null;
                }
                else
                {
                    // Console.WriteLine($"Skipping message type: {message.GetType().Name}");
                }

                // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
                // so that Npgsql can inform the server which WAL files can be removed/recycled.
                _conn.SetReplicationStatus(message.WalEnd);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
    }

    private async Task ConsumeQueueAsync()
    {
        var eventSinks = _eventSinkFactory.CreateEventSinks();
        var eventRoutes = _eventSinkFactory.GetEventRoutes();

        while (true)
        {
            if (_eventQueue.TryDequeue(out var eventData) && eventData.EventType != null)
            {
                foreach (var route in eventRoutes)
                {
                    string? eventType = Enum.GetName(typeof(EventType), eventData.EventType);
                    if (
                        eventType != null
                        && (route.EventTypes.Contains(eventType) || route.EventTypes.Contains("*"))
                    )
                    {
                        var sink = eventSinks.FirstOrDefault(s => s.Name == route.SinkName);
                        if (sink != null)
                        {
                            var cloudEvent = CreateCloudEvent(eventData, route.EventFormat);
                            await sink.SendEventAsync(cloudEvent);
                        }
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

    public enum EventType
    {
        TwinCreate,
        TwinUpdate,
        TwinDelete,
        RelationshipCreate,
        RelationshipUpdate,
        RelationshipDelete,
    }

    public class EventData()
    {
        public string? GraphName { get; set; }
        public string? TableName { get; set; }
        public EventType? EventType { get; set; }
        public JsonObject? OldValue { get; set; }
        public JsonObject? NewValue { get; set; }
    }

    public static CloudEvent CreateCloudEvent(object eventData, string eventType)
    {
        var cloudEvent = new CloudEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = new Uri("urn:source"),
            Type = eventType,
            DataContentType = "application/json",
            Data = eventData,
        };

        return cloudEvent;
    }
}
