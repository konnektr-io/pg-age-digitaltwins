using System.Text.Json;
using Npgsql;

namespace AgeDigitalTwins.Events
{
    public class DLQService(NpgsqlDataSource dataSource, ILogger? logger = null)
    {
        private readonly NpgsqlDataSource _dataSource = dataSource;
        private readonly ILogger? _logger = logger;
        private readonly string _schemaName = "digitaltwins_eventing";
        private readonly string _tableName = "dead_letter_queue";
        private readonly JsonSerializerOptions _jsonOptions =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await SchemaExistsAsync(cancellationToken))
                {
                    await CreateSchemaAsync(cancellationToken);
                }
                else
                {
                    await EnsureTableAndIndexesAsync(cancellationToken);
                }
                _logger?.LogDebug("DLQ schema initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize DLQ schema");
                throw;
            }
        }

        private async Task<bool> SchemaExistsAsync(CancellationToken cancellationToken = default)
        {
            var checkSchemaSql =
                @"SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @schemaName)";
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(checkSchemaSql, connection);
            command.Parameters.AddWithValue("@schemaName", _schemaName);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool exists && exists;
        }

        private async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
        {
            var createSchemaSql = $"CREATE SCHEMA IF NOT EXISTS {_schemaName}";
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand(createSchemaSql, connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await EnsureTableAndIndexesAsync(cancellationToken);
        }

        private async Task EnsureTableAndIndexesAsync(CancellationToken cancellationToken = default)
        {
            var createTableSql =
                $@"
                CREATE TABLE IF NOT EXISTS {_schemaName}.{_tableName} (
                    id SERIAL PRIMARY KEY,
                    event_id UUID NOT NULL,
                    sink_name TEXT NOT NULL,
                    event_type TEXT,
                    payload JSONB NOT NULL,
                    error_message TEXT,
                    error_stack TEXT,
                    retry_count INT DEFAULT 0,
                    failed_at TIMESTAMPTZ DEFAULT NOW(),
                    last_attempt_at TIMESTAMPTZ,
                    status TEXT DEFAULT 'pending'
                )";
            var createIndexSql =
                $@"
                CREATE INDEX IF NOT EXISTS idx_dlq_status ON {_schemaName}.{_tableName}(status);
                CREATE INDEX IF NOT EXISTS idx_dlq_sink ON {_schemaName}.{_tableName}(sink_name);";
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand(createTableSql, connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var command = new NpgsqlCommand(createIndexSql, connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public async Task PersistEventAsync(
            CloudNative.CloudEvents.CloudEvent cloudEvent,
            string sinkName,
            Exception ex,
            int retryCount,
            CancellationToken cancellationToken = default
        )
        {
            var eventId = cloudEvent.Id ?? Guid.NewGuid().ToString();
            var eventType = cloudEvent.Type;
            var payload = JsonSerializer.Serialize(cloudEvent, _jsonOptions);
            var errorMessage = ex.Message;
            var errorStack = ex.ToString();
            var failedAt = DateTime.UtcNow;
            var status = "pending";
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            var cmd = new NpgsqlCommand(
                $@"
                INSERT INTO {_schemaName}.{_tableName} (
                    event_id, sink_name, event_type, payload, error_message, error_stack, retry_count, failed_at, last_attempt_at, status
                ) VALUES (
                    @event_id, @sink_name, @event_type, @payload, @error_message, @error_stack, @retry_count, @failed_at, @last_attempt_at, @status
                )",
                connection
            );
            cmd.Parameters.AddWithValue("event_id", Guid.Parse(eventId));
            cmd.Parameters.AddWithValue("sink_name", sinkName ?? "unknown");
            cmd.Parameters.AddWithValue("event_type", eventType ?? "unknown");
            cmd.Parameters.AddWithValue("payload", payload);
            cmd.Parameters.AddWithValue("error_message", errorMessage ?? "");
            cmd.Parameters.AddWithValue("error_stack", errorStack ?? "");
            cmd.Parameters.AddWithValue("retry_count", retryCount);
            cmd.Parameters.AddWithValue("failed_at", failedAt);
            cmd.Parameters.AddWithValue("last_attempt_at", failedAt);
            cmd.Parameters.AddWithValue("status", status);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger?.LogInformation("Persisted event {EventId} to DLQ table.", eventId);
        }
    }
}
