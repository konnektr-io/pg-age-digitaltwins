using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.Test;

public class TestBase : IAsyncDisposable
{
    private readonly AgeDigitalTwinsClient _client;
    private static Task? _initializationTask;
    private static readonly object _initLock = new object();

    public TestBase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build();

        string connectionString =
            configuration.GetConnectionString("agedb") ?? throw new ArgumentNullException("agedb");

        var graphName = "temp_graph_" + Guid.NewGuid().ToString("N");
        _client = new AgeDigitalTwinsClient(connectionString, graphName, true, true);

        // Only initialize database once
        EnsureDatabaseInitialized();
        // Initialize graph for each test
        Console.WriteLine($"Initializing graph {graphName} ...");
        _client.InitializeGraphAsync().GetAwaiter().GetResult();
        Console.WriteLine($"Graph {graphName} initialized.");
    }

    /* private void EnsureDatabaseInitialized()
    {
        if (!_isDatabaseInitialized)
        {
            Console.WriteLine("Initializing database...");
            _client.InitializeDatabaseAsync().GetAwaiter().GetResult();
            Console.WriteLine("Database initialized.");
            _isDatabaseInitialized = true;
        }
    } */

    private void EnsureDatabaseInitialized()
    {
        if (_initializationTask == null)
        {
            lock (_initLock)
            {
                if (_initializationTask == null)
                {
                    Console.WriteLine("Initializing database...");
                    _initializationTask = Task.Run(() => _client.InitializeDatabaseAsync());
                }
            }
        }

        // Wait for the initialization to complete
        _initializationTask.GetAwaiter().GetResult();
        Console.WriteLine("Database initialized.");
    }

    public AgeDigitalTwinsClient Client => _client!;

    public async ValueTask DisposeAsync()
    {
        await _client.DropGraphAsync();
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
