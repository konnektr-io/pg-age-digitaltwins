using AgeDigitalTwins.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins.Events.Test;

/// <summary>
/// Shared fixture for events integration tests.
/// This ensures only one replication connection is created and shared across all tests.
/// </summary>
public class EventsFixture : IAsyncDisposable
{
    public AgeDigitalTwinsClient Client { get; }
    public TestingEventSink TestSink { get; }
    public AgeDigitalTwinsReplication Replication { get; }
    public TelemetryListener TelemetryListener { get; }
    public SharedEventConsumer SharedEventConsumer { get; }

    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EventsFixture> _logger;
    private readonly Task _replicationTask;
    private readonly Task _telemetryTask;
    private readonly Task _consumerTask;

    public EventsFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build();

        string connectionString =
            configuration.GetConnectionString("agedb") ?? throw new ArgumentNullException("agedb");

        var graphName = "temp_graph_events_" + Guid.NewGuid().ToString("N");

        // Setup client similar to TestBase
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);

        // UseAge(true) for CNPG images, controlled by CNPG_TEST env var
        var cnpgTest = Environment.GetEnvironmentVariable("CNPG_TEST");
        NpgsqlMultiHostDataSource dataSource;
        if (!string.IsNullOrEmpty(cnpgTest) && cnpgTest.ToLowerInvariant() == "true")
        {
            dataSource = dataSourceBuilder.UseAge(true).BuildMultiHost();
        }
        else
        {
            dataSource = dataSourceBuilder.UseAge().BuildMultiHost();
        }

        Client = new AgeDigitalTwinsClient(
            dataSource,
            new AgeDigitalTwinsClientOptions()
            {
                GraphName = graphName,
                ModelCacheExpiration = TimeSpan.Zero, // Disable model cache for tests
            }
        );

        // Setup logging
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information)
        );
        _logger = _loggerFactory.CreateLogger<EventsFixture>();

        // Setup test sink
        var testSinkLogger = _loggerFactory.CreateLogger<TestingEventSink>();
        TestSink = new TestingEventSink("test-sink", testSinkLogger);

        // Setup replication with test sink factory
        var replicationLogger = _loggerFactory.CreateLogger<AgeDigitalTwinsReplication>();
        var testSinkFactory = new TestingEventSinkFactory(configuration, _loggerFactory, TestSink);

        // Use the shared publication and slot from init.sql
        var testEventQueue = new EventQueue();
        Replication = new AgeDigitalTwinsReplication(
            connectionString,
            "age_pub", // Shared publication from init.sql
            "age_slot", // Shared slot from init.sql - this means only one connection at a time
            testEventQueue,
            replicationLogger
        );

        // Setup TelemetryListener
        var telemetryLogger = _loggerFactory.CreateLogger<TelemetryListener>();
        TelemetryListener = new TelemetryListener(
            connectionString,
            testEventQueue,
            telemetryLogger
        );

        // Setup SharedEventConsumer
        var consumerLogger = _loggerFactory.CreateLogger<SharedEventConsumer>();
        var sourceUri = new Uri("http://localhost/test");
        SharedEventConsumer = new SharedEventConsumer(testEventQueue, consumerLogger, sourceUri);

        _cancellationTokenSource = new CancellationTokenSource();

        // Start all services
        _replicationTask = Replication.RunAsync(_cancellationTokenSource.Token);
        _telemetryTask = TelemetryListener.RunAsync(_cancellationTokenSource.Token);

        // Create event routes for testing - capture all event types
        var testRoutes = new List<EventRoute>
        {
            new() { SinkName = "test-sink", EventFormat = EventFormat.EventNotification },
            new() { SinkName = "test-sink", EventFormat = EventFormat.DataHistory },
            new() { SinkName = "test-sink", EventFormat = EventFormat.Telemetry },
        };

        _consumerTask = SharedEventConsumer.ConsumeEventsAsync(
            new List<IEventSink> { TestSink },
            testRoutes,
            _cancellationTokenSource.Token
        );

        // Give replication a moment to initialize and check for immediate failures
        Task.Delay(2000).Wait();

        // Check if the task faulted immediately
        if (_replicationTask.IsFaulted)
        {
            var exception = _replicationTask.Exception?.GetBaseException();
            _logger.LogError(
                "Replication task faulted during startup: {Message}",
                exception?.Message
            );
            throw new InvalidOperationException(
                $"Replication failed to start: {exception?.Message}",
                exception
            );
        }

        _logger.LogInformation("EventsFixture initialized successfully");
    }

    /// <summary>
    /// Waits for the replication to become healthy before running tests.
    /// This should be called at the beginning of each test.
    /// </summary>
    public async Task WaitForReplicationHealthy(TimeSpan timeout = default)
    {
        if (timeout == default)
            timeout = TimeSpan.FromSeconds(30);

        var endTime = DateTime.UtcNow.Add(timeout);
        var checkCount = 0;

        _logger.LogInformation(
            "Starting to wait for replication health. Task status: {TaskStatus}",
            _replicationTask.Status
        );

        while (DateTime.UtcNow < endTime)
        {
            checkCount++;

            // Check if replication task has faulted
            if (_replicationTask.IsFaulted)
            {
                var exception = _replicationTask.Exception?.GetBaseException();
                _logger.LogError(
                    exception,
                    "Replication task faulted: {Message}",
                    exception?.Message
                );
                throw new InvalidOperationException(
                    $"Replication task faulted: {exception?.Message}",
                    exception
                );
            }

            // Check if replication task completed unexpectedly
            if (_replicationTask.IsCompleted && !_replicationTask.IsCompletedSuccessfully)
            {
                _logger.LogError(
                    "Replication task completed unexpectedly. Status: {TaskStatus}",
                    _replicationTask.Status
                );
                throw new InvalidOperationException(
                    $"Replication task completed unexpectedly with status: {_replicationTask.Status}"
                );
            }

            if (Replication.IsHealthy && TelemetryListener.IsHealthy)
            {
                _logger.LogInformation(
                    "Replication and TelemetryListener are healthy after {CheckCount} checks",
                    checkCount
                );
                return;
            }

            if (checkCount % 50 == 0) // Log every 5 seconds
            {
                _logger.LogInformation(
                    "Waiting for replication health... (check #{CheckCount}, task status: {TaskStatus})",
                    checkCount,
                    _replicationTask.Status
                );
            }

            await Task.Delay(100);
        }

        _logger.LogError("Final task status: {TaskStatus}", _replicationTask.Status);
        throw new TimeoutException(
            $"Replication did not become healthy within {timeout.TotalSeconds} seconds after {checkCount} checks. "
                + $"Task Status: {_replicationTask.Status}"
        );
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing EventsFixture...");

        _cancellationTokenSource.Cancel();

        try
        {
            // Wait for all tasks to complete or timeout
            await Task.WhenAll(
                _replicationTask.WaitAsync(TimeSpan.FromSeconds(5)),
                _telemetryTask.WaitAsync(TimeSpan.FromSeconds(5)),
                _consumerTask.WaitAsync(TimeSpan.FromSeconds(5))
            );
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

        _logger.LogInformation("EventsFixture disposed successfully");
    }
}
