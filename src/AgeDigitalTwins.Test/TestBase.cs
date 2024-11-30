using Microsoft.Extensions.Configuration;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins.Test;

public class TestBase : IAsyncLifetime
{
    private AgeDigitalTwinsClient? _client;

    private NpgsqlDataSource? _dataSource;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json").Build();

        string connectionString = configuration.GetConnectionString("AgeConnectionString")
            ?? throw new ArgumentNullException("AgeConnectionString");


        NpgsqlConnectionStringBuilder connectionStringBuilder = new(connectionString)
        {
            SearchPath = "ag_catalog, \"$user\", public"
        };

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);

        _dataSource = dataSourceBuilder
            .UseAge(false)
            .Build();

        var graphName = "temp_graph" + Guid.NewGuid().ToString("N");
        _client = new AgeDigitalTwinsClient(_dataSource, graphName);
        await _client.CreateGraphAsync();
    }

    public AgeDigitalTwinsClient Client => _client!;

    public async Task DisposeAsync()
    {
        await _client!.DropGraphAsync();
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }
}
