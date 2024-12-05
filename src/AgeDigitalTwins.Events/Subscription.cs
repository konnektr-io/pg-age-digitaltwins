using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgeDigitalTwins.Events
{
    public class AgeDigitalTwinsSubscription : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly string _replicationSlot;
        private LogicalReplicationConnection? _conn;
        private CancellationTokenSource? _cancellationTokenSource;

        public AgeDigitalTwinsSubscription(string connectionString, string replicationSlot)
        {
            _connectionString = connectionString;
            _replicationSlot = replicationSlot;
        }

        public async Task StartAsync()
        {
            _conn = new LogicalReplicationConnection(_connectionString);
            await _conn.Open();

            var slot = new PgOutputReplicationSlot(_replicationSlot);
            _cancellationTokenSource = new CancellationTokenSource();

            await foreach (var message in _conn.StartReplication(
                slot, new PgOutputReplicationOptions("blog_pub", PgOutputProtocolVersion.V3), _cancellationTokenSource.Token))
            {
                Console.WriteLine($"Received message type: {message.GetType().Name}");

                // Always call SetReplicationStatus() or assign LastAppliedLsn and LastFlushedLsn individually
                // so that Npgsql can inform the server which WAL files can be removed/recycled.
                _conn.SetReplicationStatus(message.WalEnd);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        public async ValueTask DisposeAsync()
        {
            if (_conn != null)
            {
                await _conn.DisposeAsync();
            }
            _cancellationTokenSource?.Dispose();
        }
    }
}