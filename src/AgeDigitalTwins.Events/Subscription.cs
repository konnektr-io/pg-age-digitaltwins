using Npgsql;
using Npgsql.Age.Types;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AgeDigitalTwins.Events
{
    public class AgeDigitalTwinsSubscription(string connectionString, string publication, string replicationSlot) : IAsyncDisposable
    {
        private readonly string _connectionString = connectionString;
        private readonly string _publication = publication;
        private readonly string _replicationSlot = replicationSlot;
        private LogicalReplicationConnection? _conn;
        private CancellationTokenSource? _cancellationTokenSource;

        public async Task StartAsync()
        {
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

            await foreach (PgOutputReplicationMessage message in messages)
            {
                try
                {
                    Console.WriteLine($"Received message type: {message.GetType().Name}");

                    ReplicationTuple? newRow = null;
                    string graphName;
                    string tableName;

                    if (message is InsertMessage insertMessage)
                    {
                        newRow = insertMessage.NewRow;
                        graphName = insertMessage.Relation.Namespace;
                        tableName = insertMessage.Relation.RelationName;
                        var newRowJson = await ConvertRowToJsonAsync(insertMessage.NewRow);
                        Console.WriteLine($"Inserted row: {newRowJson}");
                    }
                    else if (message is FullUpdateMessage updateMessage)
                    {
                        graphName = updateMessage.Relation.Namespace;
                        tableName = updateMessage.Relation.RelationName;
                        var oldRowJson = await ConvertRowToJsonAsync(updateMessage.OldRow);
                        var newRowJson = await ConvertRowToJsonAsync(updateMessage.NewRow);
                        Console.WriteLine($"Updated row: {oldRowJson} -> {newRowJson}");
                    }
                    else if (message is FullDeleteMessage deleteMessage)
                    {
                        graphName = deleteMessage.Relation.Namespace;
                        tableName = deleteMessage.Relation.RelationName;
                        var oldRowJson = await ConvertRowToJsonAsync(deleteMessage.OldRow);
                        Console.WriteLine($"Deleted row: {oldRowJson}");
                    }
                    else
                    {
                        Console.WriteLine($"Skipping message type: {message.GetType().Name}");
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
    }
}