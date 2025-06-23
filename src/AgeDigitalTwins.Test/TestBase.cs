using Microsoft.Extensions.Configuration;
using Npgsql;
using Npgsql.Age;

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
            configuration.GetConnectionString("agedb") ?? throw new ArgumentNullException("agedb");

        var graphName = "temp_graph_" + Guid.NewGuid().ToString("N");

        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);

        var _dataSource = dataSourceBuilder.UseAge(true).BuildMultiHost();

        _client = new AgeDigitalTwinsClient(
            _dataSource,
            new AgeDigitalTwinsClientOptions()
            {
                GraphName = graphName,
                ModelCacheExpiration =
                    TimeSpan.Zero // Disable model cache for tests
                ,
            }
        );
    }

    public AgeDigitalTwinsClient Client => _client!;

    public async ValueTask DisposeAsync()
    {
        await _client.DropGraphAsync();
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
