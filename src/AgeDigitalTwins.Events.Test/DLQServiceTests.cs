using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace AgeDigitalTwins.Events.Test;

[Trait("Category", "Integration")]
public class DLQServiceTests
{
    private static string GetTestConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build();
        return configuration.GetConnectionString("agedb")
            ?? throw new ArgumentNullException("agedb");
    }

    private readonly ILogger<DLQService> _logger = LoggerFactory
        .Create(builder => builder.AddConsole())
        .CreateLogger<DLQService>();

    private DLQService CreateDLQService()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(GetTestConnectionString());
        var dataSource = dataSourceBuilder.Build();
        return new DLQService(dataSource, _logger);
    }

    [Fact]
    public async Task InitializeSchema_CreatesSchemaAndTable()
    {
        var dlqService = CreateDLQService();
        await dlqService.InitializeSchemaAsync();
        // Verify schema exists
        await using var conn = new NpgsqlConnection(GetTestConnectionString());
        await conn.OpenAsync();
        var cmd = new NpgsqlCommand(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'digitaltwins_eventing'",
            conn
        );
        var result = await cmd.ExecuteScalarAsync();
        Assert.Equal("digitaltwins_eventing", result);
    }

    [Fact]
    public async Task PersistEventAsync_InsertsEventIntoDLQ()
    {
        var dlqService = CreateDLQService();
        await dlqService.InitializeSchemaAsync();
        var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
        {
            Type = "testType",
            Source = new Uri("urn:test"),
            Id = Guid.NewGuid().ToString(),
            Subject = "testSubject",
            Time = DateTimeOffset.UtcNow,
        };
        var testException = new Exception("Test failure");
        await dlqService.PersistEventAsync(cloudEvent, "TestSink", testException, 3);
        // Verify event is in DLQ table
        await using var conn = new NpgsqlConnection(GetTestConnectionString());
        await conn.OpenAsync();
        var cmd = new NpgsqlCommand(
            "SELECT event_id, sink_name, event_type, error_message, status FROM digitaltwins_eventing.dead_letter_queue WHERE sink_name = 'TestSink' ORDER BY failed_at DESC LIMIT 1",
            conn
        );
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.NotNull(cloudEvent.Id);
        Assert.Equal(Guid.Parse(cloudEvent.Id!), reader.GetGuid(0));
        Assert.Equal("TestSink", reader.GetString(1));
        Assert.Equal("testType", reader.GetString(2));
        Assert.Equal("Test failure", reader.GetString(3));
        Assert.Equal("pending", reader.GetString(4));

        // Cleanup: remove test event
        reader.Close();
        var cleanupCmd = new NpgsqlCommand(
            "DELETE FROM digitaltwins_eventing.dead_letter_queue WHERE event_id = @event_id",
            conn
        );
        cleanupCmd.Parameters.AddWithValue("event_id", Guid.Parse(cloudEvent.Id!));
        await cleanupCmd.ExecuteNonQueryAsync();
    }
}

