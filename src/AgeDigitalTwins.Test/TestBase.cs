using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.Test;

public class TestBase : IAsyncDisposable
{
    private readonly AgeDigitalTwinsClient _client;

    public TestBase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build();

        string connectionString =
            configuration.GetConnectionString("AgeConnectionString")
            ?? throw new ArgumentNullException("AgeConnectionString");

        var graphName = "temp_graph_" + Guid.NewGuid().ToString("N");
        _client = new AgeDigitalTwinsClient(connectionString, graphName);
    }

    public AgeDigitalTwinsClient Client => _client!;

    public async ValueTask DisposeAsync()
    {
        await _client.DropGraphAsync();
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
