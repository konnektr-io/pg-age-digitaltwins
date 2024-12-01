using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.Test;

public class TestBase
{
    private readonly AgeDigitalTwinsClient _client;

    public TestBase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json").Build();

        string connectionString = configuration.GetConnectionString("AgeConnectionString")
            ?? throw new ArgumentNullException("AgeConnectionString");

        var graphName = "temp_graph" + Guid.NewGuid().ToString("N");
        _client = new AgeDigitalTwinsClient(connectionString, graphName);
    }

    public AgeDigitalTwinsClient Client => _client!;

    public async Task DisposeAsync()
    {
        await _client.DropGraphAsync();
        await _client.DisposeAsync();
    }
}
