using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.Test;

public class TestBase : IAsyncDisposable
{
    private readonly AgeDigitalTwinsClient _client;
    private static readonly Lazy<Task> _initializationTask = new Lazy<Task>(
        InitializeDatabaseAsync
    );

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
        Console.WriteLine("Initializing database...");
        _initializationTask.Value.GetAwaiter().GetResult();
        // Initialize graph for each test
        Console.WriteLine($"Initializing graph {graphName} ...");
        _client.InitializeGraphAsync().GetAwaiter().GetResult();
        Console.WriteLine($"Graph {graphName} initialized.");
    }

    private static async Task InitializeDatabaseAsync()
    {
        // Replace with your actual initialization logic
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build();

        string connectionString =
            configuration.GetConnectionString("agedb") + ";Include Error Detail=true"
            ?? throw new ArgumentNullException("agedb");

        var client = new AgeDigitalTwinsClient(connectionString, noInitialization: true);
        await client.InitializeDatabaseAsync();
        await client.DisposeAsync();
    }

    public AgeDigitalTwinsClient Client => _client!;

    public async ValueTask DisposeAsync()
    {
        await _client.DropGraphAsync();
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
