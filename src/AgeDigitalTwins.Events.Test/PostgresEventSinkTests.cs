using System.Text.Json.Nodes;
using AgeDigitalTwins.Events.Abstractions;
using AgeDigitalTwins.Events.Sinks.Postgres;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgeDigitalTwins.Events.Test;

public class PostgresEventSinkTests
{
    private static PostgresSinkOptions CreateOptions(
        bool trackLastUpdatedBy = false,
        string? schema = null,
        string? propertyEventsTable = null,
        Dictionary<SinkEventType, string>? eventTypeMappings = null
    ) =>
        new()
        {
            Name = "PostgresDataHistory",
            ConnectionString = "Host=localhost;Port=5432;Database=history;Username=app;Password=app",
            Schema = schema,
            PropertyEventsTable = propertyEventsTable,
            TrackLastUpdatedBy = trackLastUpdatedBy,
            EventTypeMappings = eventTypeMappings,
        };

    [Fact]
    public void Constructor_WithoutConnectionString_Throws()
    {
        var options = new PostgresSinkOptions { Name = "PostgresDataHistory" };

        Assert.Throws<ArgumentException>(() =>
            new PostgresEventSink(options, NullLogger.Instance)
        );
    }

    [Fact]
    public async Task Constructor_WithConnectionString_DoesNotConnect()
    {
        // Construction must stay lazy: the database may be unreachable at startup.
        var sink = new PostgresEventSink(CreateOptions(), NullLogger.Instance);

        Assert.Equal("PostgresDataHistory", sink.Name);
        Assert.True(sink.IsHealthy);
        await sink.DisposeAsync();
    }

    [Fact]
    public void BuildTableDefinitions_UsesSnakeCaseDefaults()
    {
        var tables = PostgresEventSink.BuildTableDefinitions(CreateOptions());

        Assert.Equal(3, tables.Count);
        Assert.Equal(
            "adt_property_events",
            tables[CloudEventFactory_PropertyEventType].Table
        );
        Assert.Equal(
            "adt_twin_lifecycle_events",
            tables[CloudEventFactory_TwinLifecycleType].Table
        );
        Assert.Equal(
            "adt_relationship_lifecycle_events",
            tables[CloudEventFactory_RelationshipLifecycleType].Table
        );
        Assert.All(tables.Values, t => Assert.Equal("public", t.Schema));
    }

    [Fact]
    public void BuildTableDefinitions_WithCustomSchemaAndTable_UsesConfiguredNames()
    {
        var tables = PostgresEventSink.BuildTableDefinitions(
            CreateOptions(schema: "history", propertyEventsTable: "twin_property_events")
        );

        var table = tables[CloudEventFactory_PropertyEventType];
        Assert.Equal("history", table.Schema);
        Assert.Equal("twin_property_events", table.Table);
    }

    [Theory]
    [InlineData("public; DROP TABLE twin")]
    [InlineData("1_invalid")]
    [InlineData("with space")]
    [InlineData("quote\"d")]
    public void BuildTableDefinitions_WithInvalidIdentifier_Throws(string table)
    {
        Assert.Throws<ArgumentException>(() =>
            PostgresEventSink.BuildTableDefinitions(CreateOptions(propertyEventsTable: table))
        );
    }

    [Fact]
    public void BuildTableDefinitions_WithCustomEventTypeMappings_KeysByCloudEventType()
    {
        var tables = PostgresEventSink.BuildTableDefinitions(
            CreateOptions(
                eventTypeMappings: new Dictionary<SinkEventType, string>
                {
                    [SinkEventType.PropertyEvent] = "custom.property.event",
                }
            )
        );

        Assert.True(tables.ContainsKey("custom.property.event"));
        Assert.False(tables.ContainsKey(CloudEventFactory_PropertyEventType));
    }

    [Fact]
    public void BuildPropertyEventColumns_WhenTrackLastUpdatedByTrue_IncludesUpdatedBy()
    {
        var columns = PostgresEventSink.BuildPropertyEventColumns(trackLastUpdatedBy: true);

        var updatedBy = Assert.Single(
            columns,
            c => c.Name == "updated_by"
        );
        Assert.Equal("updatedBy", updatedBy.JsonProperty);
        Assert.Equal(PostgresColumnType.Text, updatedBy.Type);
    }

    [Fact]
    public void BuildPropertyEventColumns_WhenTrackLastUpdatedByFalse_OmitsUpdatedBy()
    {
        var columns = PostgresEventSink.BuildPropertyEventColumns(
            trackLastUpdatedBy: false
        );

        Assert.DoesNotContain(columns, c => c.Name == "updated_by");
    }

    [Fact]
    public void BuildPropertyEventColumns_AreSnakeCase()
    {
        var columns = PostgresEventSink.BuildPropertyEventColumns(trackLastUpdatedBy: false);

        Assert.Equal(
            [
                "time_stamp",
                "source_time_stamp",
                "service_id",
                "id",
                "model_id",
                "key",
                "value",
                "relationship_target",
                "relationship_id",
                "action",
            ],
            columns.Select(c => c.Name)
        );
        Assert.Equal(
            [
                "timeStamp",
                "sourceTimeStamp",
                "serviceId",
                "id",
                "modelId",
                "key",
                "value",
                "relationshipTarget",
                "relationshipId",
                "action",
            ],
            columns.Select(c => c.JsonProperty)
        );
    }

    [Fact]
    public void BuildTwinLifecycleEventColumns_WhenTrackLastUpdatedByTrue_IncludesUpdatedBy()
    {
        var columns = PostgresEventSink.BuildTwinLifecycleEventColumns(
            trackLastUpdatedBy: true
        );

        Assert.Equal(
            ["time_stamp", "service_id", "twin_id", "action", "model_id", "updated_by"],
            columns.Select(c => c.Name)
        );
    }

    [Fact]
    public void BuildRelationshipLifecycleEventColumns_WhenTrackLastUpdatedByTrue_IncludesUpdatedBy()
    {
        var columns = PostgresEventSink.BuildRelationshipLifecycleEventColumns(
            trackLastUpdatedBy: true
        );

        Assert.Equal(
            [
                "time_stamp",
                "service_id",
                "relationship_id",
                "action",
                "name",
                "source",
                "target",
                "updated_by",
            ],
            columns.Select(c => c.Name)
        );
    }

    [Fact]
    public void BuildCreateTableSql_DefinesColumnsWithPostgresTypes()
    {
        var table = PostgresEventSink.BuildTableDefinitions(
            CreateOptions(trackLastUpdatedBy: true)
        )[CloudEventFactory_PropertyEventType];

        var sql = PostgresEventSink.BuildCreateTableSql(table);

        Assert.Contains(
            "CREATE TABLE IF NOT EXISTS \"public\".\"adt_property_events\"",
            sql
        );
        Assert.Contains("\"time_stamp\" timestamptz NOT NULL", sql);
        Assert.Contains("\"source_time_stamp\" timestamptz,", sql);
        Assert.Contains("\"value\" jsonb", sql);
        Assert.Contains("\"id\" text", sql);
        Assert.Contains("\"updated_by\" text", sql);
    }

    [Fact]
    public void BuildCreateTableSql_WithoutTrackLastUpdatedBy_OmitsUpdatedBy()
    {
        var table = PostgresEventSink.BuildTableDefinitions(
            CreateOptions(trackLastUpdatedBy: false)
        )[CloudEventFactory_PropertyEventType];

        var sql = PostgresEventSink.BuildCreateTableSql(table);

        Assert.DoesNotContain("updated_by", sql);
    }

    [Fact]
    public void BuildIndexSql_IndexesTimeStampAndEntity()
    {
        var table = PostgresEventSink.BuildTableDefinitions(CreateOptions())[
            CloudEventFactory_PropertyEventType
        ];

        var sql = PostgresEventSink.BuildIndexSql(table);

        Assert.Equal(2, sql.Count);
        Assert.Contains(
            "CREATE INDEX IF NOT EXISTS \"ix_adt_property_events_time_stamp\" ON \"public\".\"adt_property_events\" (\"time_stamp\");",
            sql
        );
        Assert.Contains(
            "CREATE INDEX IF NOT EXISTS \"ix_adt_property_events_id\" ON \"public\".\"adt_property_events\" (\"id\");",
            sql
        );
    }

    [Fact]
    public void BuildHypertableSql_PrefersByRangeAndFallsBackToTimeColumn()
    {
        var table = PostgresEventSink.BuildTableDefinitions(CreateOptions())[
            CloudEventFactory_TwinLifecycleType
        ];

        var sql = PostgresEventSink.BuildHypertableSql(table);

        Assert.Equal(2, sql.Count);
        Assert.Equal(
            "SELECT create_hypertable('\"public\".\"adt_twin_lifecycle_events\"', by_range('time_stamp'), if_not_exists => TRUE, migrate_data => TRUE);",
            sql[0]
        );
        Assert.Equal(
            "SELECT create_hypertable('\"public\".\"adt_twin_lifecycle_events\"', 'time_stamp', if_not_exists => TRUE, migrate_data => TRUE);",
            sql[1]
        );
    }

    [Fact]
    public void AsText_RendersValuesWithoutJsonQuotes()
    {
        var row = new JsonObject
        {
            ["id"] = "twin-1",
            ["count"] = 42,
            ["enabled"] = true,
            ["value"] = new JsonObject { ["temperature"] = 21.5 },
        };

        Assert.Equal("twin-1", PostgresEventSink.AsText(row["id"]));
        Assert.Equal("42", PostgresEventSink.AsText(row["count"]));
        Assert.Equal("true", PostgresEventSink.AsText(row["enabled"]));
        Assert.Equal(
            "{\"temperature\":21.5}",
            PostgresEventSink.AsText(row["value"])
        );
        Assert.Null(PostgresEventSink.AsText(null));
    }

    [Fact]
    public void ParseTimestamp_HandlesNativeTimestampsAndIsoStrings()
    {
        var expected = new DateTimeOffset(2026, 9, 13, 9, 16, 11, TimeSpan.Zero);

        var native = new JsonObject { ["timeStamp"] = expected };
        var iso = new JsonObject { ["timeStamp"] = "2026-09-13T09:16:11Z" };

        Assert.Equal(expected, PostgresEventSink.ParseTimestamp(native["timeStamp"]));
        Assert.Equal(expected, PostgresEventSink.ParseTimestamp(iso["timeStamp"]));
        Assert.Null(PostgresEventSink.ParseTimestamp(null));
        Assert.Null(PostgresEventSink.ParseTimestamp(JsonValue.Create("not a timestamp")));
    }

    private const string CloudEventFactory_PropertyEventType = "Konnektr.Graph.Property.Event";
    private const string CloudEventFactory_TwinLifecycleType = "Konnektr.Graph.Twin.Lifecycle";
    private const string CloudEventFactory_RelationshipLifecycleType =
        "Konnektr.Graph.Relationship.Lifecycle";
}
