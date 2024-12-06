using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgeDigitalTwins.Events;

public class AgeDigitalTwinsSubscription(string connectionString, string publication, string replicationSlot) : IAsyncDisposable
{
    private readonly string _connectionString = connectionString;
    private readonly string _publication = publication;
    private readonly string _replicationSlot = replicationSlot;
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
            slot, new PgOutputReplicationOptions(_publication, PgOutputProtocolVersion.V4), _cancellationTokenSource.Token);

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
                    currentEvent.EventType = "Create";
                    currentEvent.GraphName = insertMessage.Relation.Namespace;
                    currentEvent.TableName = insertMessage.Relation.RelationName;
                    currentEvent.NewValue = await ConvertRowToJsonAsync(insertMessage.NewRow);
                    // Console.WriteLine($"Inserted row: {currentEvent.NewValue}");
                }
                else if (message is FullUpdateMessage updateMessage)
                {
                    if (currentEvent == null)
                    {
                        Console.WriteLine("Skipping update message without a transaction");
                        continue;
                    }
                    currentEvent.EventType ??= "Update";
                    currentEvent.GraphName = updateMessage.Relation.Namespace;
                    currentEvent.TableName = updateMessage.Relation.RelationName;
                    currentEvent.OldValue ??= await ConvertRowToJsonAsync(updateMessage.OldRow);
                    currentEvent.NewValue = await ConvertRowToJsonAsync(updateMessage.NewRow);
                    // Console.WriteLine($"Updated row: {currentEvent.OldValue} -> {currentEvent.NewValue}");
                }
                else if (message is FullDeleteMessage deleteMessage)
                {
                    if (currentEvent == null)
                    {
                        Console.WriteLine("Skipping delete message without a transaction");
                        continue;
                    }
                    currentEvent.EventType = "Delete";
                    currentEvent.GraphName = deleteMessage.Relation.Namespace;
                    currentEvent.TableName = deleteMessage.Relation.RelationName;
                    currentEvent.OldValue = await ConvertRowToJsonAsync(deleteMessage.OldRow);
                    // Console.WriteLine($"Deleted row: {currentEvent.OldValue}");
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
        while (true)
        {
            if (_eventQueue.TryDequeue(out var eventData))
            {
                // Log the event data
                Console.WriteLine($"Event Type: {eventData.EventType}");
                Console.WriteLine($"Graph Name: {eventData.GraphName}");
                Console.WriteLine($"Table Name: {eventData.TableName}");
                Console.WriteLine($"Old Value: {eventData.OldValue}");
                Console.WriteLine($"New Value: {eventData.NewValue}");
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
            if (value.GetFieldName() != "properties" || value.GetDataTypeName() != "ag_catalog.agtype")
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


    public class EventData()
    {
        public string? GraphName { get; set; }
        public string? TableName { get; set; }
        public string? EventType { get; set; }
        public JsonObject? OldValue { get; set; }
        public JsonObject? NewValue { get; set; }
    }
}
