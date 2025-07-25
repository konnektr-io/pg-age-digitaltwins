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
    private readonly Task _replicationTask;

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

        // Start replication - RunAsync should handle its own background tasks
        _replicationTask = Replication.RunAsync(_cancellationTokenSource.Token);

        // Give replication a moment to initialize
        Task.Delay(1000).Wait();
    }

    /// <summary>
    /// Verifies that the PostgreSQL replication setup is correct.
    /// </summary>
    protected async Task VerifyReplicationSetup()
    {
        await using var connection = await Client.GetDataSource().OpenConnectionAsync();
        
        // Check if the publication exists
        await using var pubCommand = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM pg_publication WHERE pubname = 'age_pub')",
            connection
        );
        var pubExists = (bool)(await pubCommand.ExecuteScalarAsync() ?? false);
        Console.WriteLine($"Publication 'age_pub' exists: {pubExists}");
        
        // Check if the replication slot exists
        await using var slotCommand = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM pg_replication_slots WHERE slot_name = 'age_slot')",
            connection
        );
        var slotExists = (bool)(await slotCommand.ExecuteScalarAsync() ?? false);
        Console.WriteLine($"Replication slot 'age_slot' exists: {slotExists}");
        
        // Check WAL level
        await using var walCommand = new NpgsqlCommand(
            "SHOW wal_level",
            connection
        );
        var walLevel = await walCommand.ExecuteScalarAsync();
        Console.WriteLine($"WAL level: {walLevel}");
        
        // Check max_replication_slots
        await using var maxSlotsCommand = new NpgsqlCommand(
            "SHOW max_replication_slots",
            connection
        );
        var maxSlots = await maxSlotsCommand.ExecuteScalarAsync();
        Console.WriteLine($"Max replication slots: {maxSlots}");
        
        if (!pubExists || !slotExists)
        {
            throw new InvalidOperationException($"Replication setup incomplete. Publication exists: {pubExists}, Slot exists: {slotExists}");
        }
    }

    /// <summary>
    /// Waits for the replication to become healthy before running tests.
    /// </summary>
    protected async Task WaitForReplicationHealthy(TimeSpan timeout = default)
    {
        if (timeout == default)
            timeout = TimeSpan.FromSeconds(30); // Increased timeout

        // First, verify the replication setup
        Console.WriteLine("Verifying replication setup...");
        await VerifyReplicationSetup();

        var endTime = DateTime.UtcNow.Add(timeout);
        var checkCount = 0;

        Console.WriteLine($"Starting to wait for replication health. Task status: {_replicationTask.Status}");

        while (DateTime.UtcNow < endTime)
        {
            checkCount++;

            // Check if replication task has faulted
            if (_replicationTask.IsFaulted)
            {
                var exception = _replicationTask.Exception?.GetBaseException();
                Console.WriteLine($"Replication task faulted: {exception?.Message}");
                Console.WriteLine($"Exception details: {exception}");
                throw new InvalidOperationException(
                    $"Replication task faulted: {exception?.Message}", exception
                );
            }

            // Check if replication task completed unexpectedly
            if (_replicationTask.IsCompleted && !_replicationTask.IsCompletedSuccessfully)
            {
                Console.WriteLine($"Replication task completed unexpectedly. Status: {_replicationTask.Status}");
                throw new InvalidOperationException(
                    $"Replication task completed unexpectedly with status: {_replicationTask.Status}"
                );
            }

            if (Replication.IsHealthy)
            {
                Console.WriteLine($"Replication became healthy after {checkCount} checks");
                return;
            }

            if (checkCount % 50 == 0) // Log every 5 seconds
            {
                Console.WriteLine($"Waiting for replication health... (check #{checkCount}, task status: {_replicationTask.Status})");
            }

            await Task.Delay(100);
        }

        Console.WriteLine($"Final task status: {_replicationTask.Status}");
        throw new TimeoutException(
            $"Replication did not become healthy within {timeout.TotalSeconds} seconds after {checkCount} checks. "
                + $"Task Status: {_replicationTask.Status}"
        );
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();

        try
        {
            // Wait for replication task to complete or timeout
            await _replicationTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore task completion errors
        }

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
