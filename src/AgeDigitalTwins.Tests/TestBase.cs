using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.Tests;

public class TestBase : IAsyncDisposable, IDisposable
{
    private readonly AgeDigitalTwinsClient _client;

    public TestBase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json").Build();

        string connectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
            ?? configuration.GetConnectionString("AgeConnectionString")
            ?? throw new ArgumentNullException("AgeConnectionString");

        var graphName = "temp_graph" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
        _client = new AgeDigitalTwinsClient(connectionString, new() { GraphName = graphName });
        _client.CreateGraphAsync().GetAwaiter().GetResult();
    }

    public AgeDigitalTwinsClient Client => _client;

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DropGraphAsync();
        GC.SuppressFinalize(this);
    }
}