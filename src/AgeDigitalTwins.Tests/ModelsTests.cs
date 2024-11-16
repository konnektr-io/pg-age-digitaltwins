using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace AgeDigitalTwins.Tests;

public class DigitalTwinsClientTests
{
    private readonly AgeDigitalTwinsClient _client;
    private readonly string _graphName;

    public DigitalTwinsClientTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json").Build();

        string connectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
            ?? configuration.GetConnectionString("AgeConnectionString")
            ?? throw new ArgumentNullException("AgeConnectionString");
        _client = new AgeDigitalTwinsClient(connectionString, new());

        _graphName = "temp_graph" + DateTime.Now.ToString("yyyyMMddHHmmssffff");
        _client.CreateGraphAsync(_graphName).GetAwaiter().GetResult();
    }

    internal void Dispose()
    {
        _client.DropGraphAsync(_graphName).GetAwaiter().GetResult();
        _client.Dispose();
    }

    [Fact]
    public async Task CreateModels_ValidatesAndCreatesSimpleModel()
    {
        var result = await _client.CreateModelsAsync([SampleData.DtdlSample]);
        Console.WriteLine(result);
    }

    /* [Fact]
    public async void Test2()
    {
        var result = await _client.GetDigitalTwinAsync("unittest");
        Console.WriteLine(result);
    } */
}
