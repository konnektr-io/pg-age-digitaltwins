using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.Test;

public class TestBase : IAsyncDisposable
{
    private readonly AgeDigitalTwinsClient _client;
    private static bool _isDatabaseInitialized = false;
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
        _client.InitializeGraphAsync().GetAwaiter().GetResult();
    }

    private void EnsureDatabaseInitialized()
    {
        lock (_initLock)
        {
            if (!_isDatabaseInitialized)
            {
                _client.InitializeDatabaseAsync().GetAwaiter().GetResult();
                _isDatabaseInitialized = true;
            }
        }
    }

    public AgeDigitalTwinsClient Client => _client!;

    public async ValueTask DisposeAsync()
    {
        await _client.DropGraphAsync();
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
