using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgeDigitalTwins.Events.Abstractions;
using AgeDigitalTwins.Events.Core.Events;
using CloudNative.CloudEvents;
using Npgsql;
using NpgsqlTypes;

namespace AgeDigitalTwins.Events.Sinks.Postgres;

/// <summary>
/// Storage type of a data history column.
/// </summary>
internal enum PostgresColumnType
{
    Text,
    TimestampTz,
    Jsonb,
}

/// <summary>
/// Maps a data history event body property to a PostgreSQL column.
/// </summary>
internal sealed record PostgresColumn(
    string Name,
    string JsonProperty,
    PostgresColumnType Type,
    bool RequiresTrackLastUpdatedBy = false
);

/// <summary>
/// A data history table (one per data history event type) and its columns.
/// </summary>
internal sealed record PostgresTableDefinition(
    string EventType,
    string Schema,
    string Table,
    string EntityColumn,
    IReadOnlyList<PostgresColumn> Columns
);

/// <summary>
/// Writes data history (property, twin lifecycle and relationship lifecycle) events to
/// PostgreSQL, using TimescaleDB hypertables when the extension is available.
/// Table and column names follow PostgreSQL conventions (snake_case).
/// </summary>
public class PostgresEventSink : IEventSink, IAsyncDisposable
{
    internal const string DefaultSchema = "public";
    internal const string DefaultPropertyEventsTable = "adt_property_events";
    internal const string DefaultTwinLifeCycleEventsTable = "adt_twin_lifecycle_events";
    internal const string DefaultRelationshipLifeCycleEventsTable =
        "adt_relationship_lifecycle_events";

    internal const string TimeStampColumn = "time_stamp";

    private static readonly Regex IdentifierRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled
    );

    private readonly PostgresSinkOptions _options;
    private readonly ILogger _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly Dictionary<string, PostgresTableDefinition> _tables;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;
    private bool _isHealthy = true;

    public PostgresEventSink(PostgresSinkOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException(
                "A connection string is required. Configure 'ConnectionString' on the sink or provide the graph connection string (ConnectionStrings:agedb).",
                nameof(options)
            );
        }

        _dataSource = new NpgsqlDataSourceBuilder(options.ConnectionString).Build();
        _tables = BuildTableDefinitions(options);
    }

    public string Name => _options.Name;

    /// <summary>
    /// Indicates whether the last write to the database succeeded.
    /// </summary>
    public bool IsHealthy => _isHealthy;

    /// <summary>
    /// Data history tables this sink writes to, keyed by CloudEvent type.
    /// </summary>
    internal IReadOnlyDictionary<string, PostgresTableDefinition> Tables => _tables;

    public async Task SendEventsAsync(
        IEnumerable<CloudEvent> cloudEvents,
        CancellationToken cancellationToken = default
    )
    {
        var events = cloudEvents as ICollection<CloudEvent> ?? cloudEvents.ToList();
        if (events.Count == 0)
        {
            return;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);

            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(
                cancellationToken
            );

            foreach (var eventGroup in events.GroupBy(e => e.Type))
            {
                var eventType = eventGroup.Key;
                if (string.IsNullOrEmpty(eventType))
                {
                    _logger.LogWarning(
                        "Skipping event for sink '{SinkName}': Event type is null",
                        Name
                    );
                    continue;
                }

                if (!_tables.TryGetValue(eventType, out PostgresTableDefinition? table))
                {
                    _logger.LogWarning(
                        "Skipping event for sink '{SinkName}': Unsupported event type: {EventType}",
                        Name,
                        eventType
                    );
                    continue;
                }

                var rows = new List<JsonObject>();
                foreach (var cloudEvent in eventGroup)
                {
                    if (cloudEvent.Data is JsonObject data)
                    {
                        rows.Add(data);
                    }
                    else
                    {
                        _logger.LogError(
                            "Skipping event for sink '{SinkName}': Data must be a JSON object",
                            Name
                        );
                    }
                }

                if (rows.Count == 0)
                {
                    continue;
                }

                await CopyAsync(connection, table, rows, cancellationToken);

                _isHealthy = true;
                _logger.LogInformation(
                    "Wrote {EventCount} event(s) of type {EventType} to table {TableName} for sink '{SinkName}'",
                    rows.Count,
                    eventType,
                    QualifiedTableName(table),
                    Name
                );
            }
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _logger.LogError(
                ex,
                "Write to Postgres sink {SinkName} failed: {Reason}",
                Name,
                ex.Message
            );
            throw;
        }
    }

    /// <summary>
    /// Creates the data history tables (and hypertables when TimescaleDB is installed).
    /// Runs once per sink instance, lazily on the first event batch.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(
                cancellationToken
            );

            var tables = _tables.Values.DistinctBy(t => $"{t.Schema}.{t.Table}").ToList();

            foreach (var table in tables)
            {
                if (!string.Equals(table.Schema, "public", StringComparison.Ordinal))
                {
                    await ExecuteAsync(
                        connection,
                        $"CREATE SCHEMA IF NOT EXISTS {QuoteIdentifier(table.Schema)};",
                        cancellationToken
                    );
                }

                await ExecuteAsync(connection, BuildCreateTableSql(table), cancellationToken);

                foreach (var indexSql in BuildIndexSql(table))
                {
                    await ExecuteAsync(connection, indexSql, cancellationToken);
                }

                _logger.LogDebug(
                    "Ensured data history table {TableName} exists for sink '{SinkName}'",
                    QualifiedTableName(table),
                    Name
                );
            }

            if (await IsTimescaleAvailableAsync(connection, cancellationToken))
            {
                foreach (var table in tables)
                {
                    await TryCreateHypertableAsync(connection, table, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation(
                    "TimescaleDB extension is not installed in the target database of sink '{SinkName}'. Using plain PostgreSQL tables. Run 'CREATE EXTENSION timescaledb;' to enable hypertables.",
                    Name
                );
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task TryCreateHypertableAsync(
        NpgsqlConnection connection,
        PostgresTableDefinition table,
        CancellationToken cancellationToken
    )
    {
        // TimescaleDB 2.13+ prefers by_range(); older releases only accept the
        // time column name. Try the modern form first and fall back to the classic one.
        foreach (var sql in BuildHypertableSql(table))
        {
            try
            {
                await ExecuteAsync(connection, sql, cancellationToken);
                _logger.LogInformation(
                    "Created TimescaleDB hypertable for {TableName} of sink '{SinkName}'",
                    QualifiedTableName(table),
                    Name
                );
                return;
            }
            catch (PostgresException ex)
            {
                _logger.LogDebug(
                    ex,
                    "Could not create hypertable for {TableName} with '{Sql}': {Reason}",
                    QualifiedTableName(table),
                    sql,
                    ex.Message
                );
            }
        }

        _logger.LogWarning(
            "TimescaleDB is installed but no hypertable could be created for {TableName} of sink '{SinkName}'. Events are stored in a plain table.",
            QualifiedTableName(table),
            Name
        );
    }

    private async Task CopyAsync(
        NpgsqlConnection connection,
        PostgresTableDefinition table,
        List<JsonObject> rows,
        CancellationToken cancellationToken
    )
    {
        var columns = string.Join(", ", table.Columns.Select(c => QuoteIdentifier(c.Name)));
        var copyCommand =
            $"COPY {QualifiedTableName(table)} ({columns}) FROM STDIN (FORMAT BINARY)";

        await using var writer = await connection.BeginBinaryImportAsync(
            copyCommand,
            cancellationToken
        );

        foreach (var row in rows)
        {
            await writer.StartRowAsync(cancellationToken);
            foreach (var column in table.Columns)
            {
                await WriteColumnAsync(writer, column, row, cancellationToken);
            }
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken
    )
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsTimescaleAvailableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken
    )
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb');",
            connection
        );
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    internal static async Task WriteColumnAsync(
        NpgsqlBinaryImporter writer,
        PostgresColumn column,
        JsonObject row,
        CancellationToken cancellationToken
    )
    {
        row.TryGetPropertyValue(column.JsonProperty, out JsonNode? node);

        switch (column.Type)
        {
            case PostgresColumnType.TimestampTz:
                var timestamp = ParseTimestamp(node);
                if (timestamp is null)
                {
                    await writer.WriteNullAsync(cancellationToken);
                }
                else
                {
                    await writer.WriteAsync(timestamp.Value, NpgsqlDbType.TimestampTz, cancellationToken);
                }
                break;

            case PostgresColumnType.Jsonb:
                if (node is null)
                {
                    await writer.WriteNullAsync(cancellationToken);
                }
                else
                {
                    await writer.WriteAsync(node.ToJsonString(), NpgsqlDbType.Jsonb, cancellationToken);
                }
                break;

            default:
                var text = AsText(node);
                if (text is null)
                {
                    await writer.WriteNullAsync(cancellationToken);
                }
                else
                {
                    await writer.WriteAsync(text, NpgsqlDbType.Text, cancellationToken);
                }
                break;
        }
    }

    /// <summary>
    /// Renders a JSON value as text. Strings are returned verbatim (without JSON quotes).
    /// </summary>
    internal static string? AsText(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue(out string? text))
        {
            return text;
        }

        return node is JsonValue ? node.ToString() : node.ToJsonString();
    }

    /// <summary>
    /// Parses a timestamp from an event body value, which is either a native JSON
    /// timestamp or an ISO-8601 string.
    /// </summary>
    internal static DateTimeOffset? ParseTimestamp(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue(out DateTimeOffset timestamp))
            {
                return timestamp;
            }

            if (value.TryGetValue(out DateTime dateTime))
            {
                return dateTime.Kind == DateTimeKind.Unspecified
                    ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc))
                    : new DateTimeOffset(dateTime);
            }
        }

        var raw = AsText(node);
        if (
            !string.IsNullOrWhiteSpace(raw)
            && DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed
            )
        )
        {
            return parsed;
        }

        return null;
    }

    internal static Dictionary<string, PostgresTableDefinition> BuildTableDefinitions(
        PostgresSinkOptions options
    )
    {
        var schema = string.IsNullOrWhiteSpace(options.Schema) ? DefaultSchema : options.Schema;
        ValidateIdentifier(schema, "Schema");

        var propertyEventsTable = ValidateIdentifier(
            options.PropertyEventsTable ?? DefaultPropertyEventsTable,
            "PropertyEventsTable"
        );
        var twinLifeCycleEventsTable = ValidateIdentifier(
            options.TwinLifeCycleEventsTable ?? DefaultTwinLifeCycleEventsTable,
            "TwinLifeCycleEventsTable"
        );
        var relationshipLifeCycleEventsTable = ValidateIdentifier(
            options.RelationshipLifeCycleEventsTable ?? DefaultRelationshipLifeCycleEventsTable,
            "RelationshipLifeCycleEventsTable"
        );

        var bySinkEventType = new Dictionary<SinkEventType, PostgresTableDefinition>
        {
            [SinkEventType.PropertyEvent] = new(
                string.Empty,
                schema,
                propertyEventsTable,
                "id",
                BuildPropertyEventColumns(options.TrackLastUpdatedBy)
            ),
            [SinkEventType.TwinLifecycle] = new(
                string.Empty,
                schema,
                twinLifeCycleEventsTable,
                "twin_id",
                BuildTwinLifecycleEventColumns(options.TrackLastUpdatedBy)
            ),
            [SinkEventType.RelationshipLifecycle] = new(
                string.Empty,
                schema,
                relationshipLifeCycleEventsTable,
                "relationship_id",
                BuildRelationshipLifecycleEventColumns(options.TrackLastUpdatedBy)
            ),
        };

        var eventTypeMappings =
            options.EventTypeMappings ?? CloudEventFactory.DefaultDataHistoryTypeMapping;

        var tables = new Dictionary<string, PostgresTableDefinition>();
        foreach (var mapping in eventTypeMappings)
        {
            if (bySinkEventType.TryGetValue(mapping.Key, out PostgresTableDefinition? table))
            {
                tables[mapping.Value] = table with { EventType = mapping.Value };
            }
        }

        return tables;
    }

    internal static List<PostgresColumn> BuildPropertyEventColumns(bool trackLastUpdatedBy)
    {
        var columns = new List<PostgresColumn>
        {
            new(TimeStampColumn, "timeStamp", PostgresColumnType.TimestampTz),
            new("source_time_stamp", "sourceTimeStamp", PostgresColumnType.TimestampTz),
            new("service_id", "serviceId", PostgresColumnType.Text),
            new("id", "id", PostgresColumnType.Text),
            new("model_id", "modelId", PostgresColumnType.Text),
            new("key", "key", PostgresColumnType.Text),
            new("value", "value", PostgresColumnType.Jsonb),
            new("relationship_target", "relationshipTarget", PostgresColumnType.Text),
            new("relationship_id", "relationshipId", PostgresColumnType.Text),
            new("action", "action", PostgresColumnType.Text),
        };

        AddUpdatedBy(columns, trackLastUpdatedBy);
        return columns;
    }

    internal static List<PostgresColumn> BuildTwinLifecycleEventColumns(bool trackLastUpdatedBy)
    {
        var columns = new List<PostgresColumn>
        {
            new(TimeStampColumn, "timeStamp", PostgresColumnType.TimestampTz),
            new("service_id", "serviceId", PostgresColumnType.Text),
            new("twin_id", "twinId", PostgresColumnType.Text),
            new("action", "action", PostgresColumnType.Text),
            new("model_id", "modelId", PostgresColumnType.Text),
        };

        AddUpdatedBy(columns, trackLastUpdatedBy);
        return columns;
    }

    internal static List<PostgresColumn> BuildRelationshipLifecycleEventColumns(
        bool trackLastUpdatedBy
    )
    {
        var columns = new List<PostgresColumn>
        {
            new(TimeStampColumn, "timeStamp", PostgresColumnType.TimestampTz),
            new("service_id", "serviceId", PostgresColumnType.Text),
            new("relationship_id", "relationshipId", PostgresColumnType.Text),
            new("action", "action", PostgresColumnType.Text),
            new("name", "name", PostgresColumnType.Text),
            new("source", "source", PostgresColumnType.Text),
            new("target", "target", PostgresColumnType.Text),
        };

        AddUpdatedBy(columns, trackLastUpdatedBy);
        return columns;
    }

    private static void AddUpdatedBy(List<PostgresColumn> columns, bool trackLastUpdatedBy)
    {
        if (trackLastUpdatedBy)
        {
            columns.Add(
                new("updated_by", "updatedBy", PostgresColumnType.Text, RequiresTrackLastUpdatedBy: true)
            );
        }
    }

    internal static string BuildCreateTableSql(PostgresTableDefinition table)
    {
        var builder = new StringBuilder();
        builder
            .Append("CREATE TABLE IF NOT EXISTS ")
            .Append(QualifiedTableName(table))
            .AppendLine(" (");

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            builder
                .Append("    ")
                .Append(QuoteIdentifier(column.Name))
                .Append(' ')
                .Append(SqlType(column.Type));

            if (string.Equals(column.Name, TimeStampColumn, StringComparison.Ordinal))
            {
                builder.Append(" NOT NULL");
            }

            builder.AppendLine(i == table.Columns.Count - 1 ? string.Empty : ",");
        }

        builder.Append(");");
        return builder.ToString();
    }

    internal static List<string> BuildIndexSql(PostgresTableDefinition table)
    {
        var tableName = table.Table;

        return
        [
            $"CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{tableName}_{TimeStampColumn}")} ON {QualifiedTableName(table)} ({QuoteIdentifier(TimeStampColumn)});",
            $"CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{tableName}_{table.EntityColumn}")} ON {QualifiedTableName(table)} ({QuoteIdentifier(table.EntityColumn)});",
        ];
    }

    /// <summary>
    /// Hypertable creation statements, most preferred first - the modern
    /// <c>by_range()</c> dimension followed by the classic time column form.
    /// </summary>
    internal static List<string> BuildHypertableSql(PostgresTableDefinition table)
    {
        // create_hypertable() takes the relation name as a string literal; identifiers are
        // validated, so the double-quoted name can be embedded verbatim.
        var relation = $"'{QualifiedTableName(table)}'";

        return
        [
            $"SELECT create_hypertable({relation}, by_range('{TimeStampColumn}'), if_not_exists => TRUE, migrate_data => TRUE);",
            $"SELECT create_hypertable({relation}, '{TimeStampColumn}', if_not_exists => TRUE, migrate_data => TRUE);",
        ];
    }

    internal static string SqlType(PostgresColumnType type) =>
        type switch
        {
            PostgresColumnType.TimestampTz => "timestamptz",
            PostgresColumnType.Jsonb => "jsonb",
            _ => "text",
        };

    internal static string QualifiedTableName(PostgresTableDefinition table) =>
        $"{QuoteIdentifier(table.Schema)}.{QuoteIdentifier(table.Table)}";

    internal static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    internal static string ValidateIdentifier(string identifier, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !IdentifierRegex.IsMatch(identifier))
        {
            throw new ArgumentException(
                $"'{identifier}' is not a valid PostgreSQL identifier. Use letters, digits and underscores, starting with a letter or underscore.",
                parameterName
            );
        }

        return identifier;
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        _initializationLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
