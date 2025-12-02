using Microsoft.Extensions.Configuration;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins.Test;

public class TestBase : IAsyncDisposable
{
    private readonly AgeDigitalTwinsClient _client;
    private readonly NpgsqlMultiHostDataSource _dataSource;

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

        // UseAge(true) for CNPG images, controlled by CNPG_TEST env var
        var cnpgTest = Environment.GetEnvironmentVariable("CNPG_TEST");
        if (!string.IsNullOrEmpty(cnpgTest) && cnpgTest.ToLowerInvariant() == "true")
        {
            _dataSource = dataSourceBuilder.UseAge(true).BuildMultiHost();
        }
        else
        {
            _dataSource = dataSourceBuilder.UseAge().BuildMultiHost();
        }

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
        // Clean up jobs schema and locks
        try
        {
            await using var connection = await _client.GetDataSource().OpenConnectionAsync();
            var jobsSchemaName = $"{_client.GetGraphName()}_jobs";

            // Drop the jobs schema if it exists
            await using var command = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS {jobsSchemaName} CASCADE",
                connection
            );
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }

        await _client.DropGraphAsync();
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
