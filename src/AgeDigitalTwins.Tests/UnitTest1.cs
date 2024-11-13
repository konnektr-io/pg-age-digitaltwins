using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace AgeDigitalTwins.IntegrationTests;

public class DigitalTwinsClientTests
{
    private readonly AgeDigitalTwinsClient _client;

    public DigitalTwinsClientTests()
    {
        var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.Development.json").Build();

        string connectionString = configuration.GetConnectionString("AgeConnectionString") ?? throw new ArgumentNullException("AgeConnectionString");
        _client = new AgeDigitalTwinsClient(connectionString, new());
    }

    [Fact]
    public async void Test1()
    {
        var result = await _client.CreateOrReplaceDigitalTwinAsync("unittest");
        Console.WriteLine(result);
    }

    [Fact]
    public async void Test2()
    {
        var result = await _client.GetDigitalTwinAsync("unittest");
        Console.WriteLine(result);
    }
}
