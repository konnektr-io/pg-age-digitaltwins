using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins.Events.Test;

/// <summary>
/// Base class for events integration tests.
/// Sets up both the AgeDigitalTwinsClient and the replication system for testing.
/// Note: Only one instance can run at a time due to shared publication and slot.
/// </summary>
public class EventsTestBase : IAsyncDisposable
{
    protected readonly AgeDigitalTwinsClient Client;
    protected readonly TestingEventSink TestSink;
    protected readonly AgeDigitalTwinsReplication Replication;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILoggerFactory _loggerFactory;

    public EventsTestBase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build();

        string connectionString =
            configuration.GetConnectionString("agedb") ?? throw new ArgumentNullException("agedb");

        var graphName = "temp_graph_" + Guid.NewGuid().ToString("N");

        // Setup client similar to TestBase
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);

        var dataSource = dataSourceBuilder.UseAge(true).BuildMultiHost();

        Client = new AgeDigitalTwinsClient(
            dataSource,
            new AgeDigitalTwinsClientOptions()
            {
                GraphName = graphName,
                ModelCacheExpiration =
                    TimeSpan.Zero // Disable model cache for tests
                ,
            }
        );

        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug)
        );

        // Setup test sink
        var testSinkLogger = _loggerFactory.CreateLogger<TestingEventSink>();
        TestSink = new TestingEventSink("test-sink", testSinkLogger);

        // Setup replication with test sink factory
        var replicationLogger = _loggerFactory.CreateLogger<AgeDigitalTwinsReplication>();
        var testSinkFactory = new TestingEventSinkFactory(configuration, _loggerFactory, TestSink);

        // Use the shared publication and slot from init.sql
        Replication = new AgeDigitalTwinsReplication(
            connectionString,
            "age_pub", // Shared publication from init.sql
            "age_slot", // Shared slot from init.sql - this means only one test can run at a time
            null, // Let it determine source URI automatically
            testSinkFactory,
            replicationLogger
        );

        _cancellationTokenSource = new CancellationTokenSource();

        // Start replication in background
        _ = Task.Run(() => Replication.RunAsync(_cancellationTokenSource.Token));

        // Wait a moment for replication to start
        Thread.Sleep(1000);
    }

    /// <summary>
    /// Waits for the replication to become healthy before running tests.
    /// </summary>
    protected async Task WaitForReplicationHealthy(TimeSpan timeout = default)
    {
        if (timeout == default)
            timeout = TimeSpan.FromSeconds(20);

        var endTime = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTime)
        {
            if (Replication.IsHealthy)
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException("Replication did not become healthy within the timeout period");
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();

        try
        {
            await Replication.DisposeAsync();
        }
        catch
        {
            // Ignore disposal errors
        }

        // Clean up jobs schema and locks similar to TestBase
        try
        {
            await using var connection = await Client.GetDataSource().OpenConnectionAsync();
            var jobsSchemaName = $"{Client.GetGraphName()}_jobs";

            // Drop the jobs schema if it exists
            await using var command = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS {jobsSchemaName} CASCADE",
                connection
            );
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }

        await Client.DropGraphAsync();
        await Client.DisposeAsync();
        _loggerFactory.Dispose();
        GC.SuppressFinalize(this);
    }
}
