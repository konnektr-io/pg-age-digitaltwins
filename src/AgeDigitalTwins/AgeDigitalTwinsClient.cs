using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using AgeDigitalTwins.Validation;
using DTDLParser;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient : IAsyncDisposable
{
    private readonly NpgsqlMultiHostDataSource _dataSource;

    private readonly string _graphName;

    private readonly MemoryCache _modelCache = new MemoryCache(new MemoryCacheOptions());

    private readonly ModelParser _modelParser;

    private readonly JsonSerializerOptions serializerOptions =
        new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly ActivitySource ActivitySource = new("AgeDigitalTwins.SDK", "1.0.0");

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeDigitalTwinsClient"/> class with a data source and graph name.
    /// </summary>
    /// <param name="dataSource">The data source for connecting to the database.</param>
    /// <param name="graphName">The name of the graph to use. Defaults to "digitaltwins".</param>
    /// <param name="noInitialization">If true, skips the initialization of the database and graph.</param>
    public AgeDigitalTwinsClient(
        NpgsqlMultiHostDataSource dataSource,
        string graphName = "digitaltwins"
    )
    {
        _graphName = graphName;
        _dataSource = dataSource;
        _modelParser = new(
            new ParsingOptions()
            {
                MaxDtdlVersion = 4,
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(_graphName, _modelCache, dtmis, ct),
            }
        );
        InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeDigitalTwinsClient"/> class with a connection string and graph name.
    /// </summary>
    /// <param name="connectionString">The connection string for the database.</param>
    /// <param name="graphName">The name of the graph to use. Defaults to "digitaltwins".</param>
    /// <param name="loadAgeFromPlugins">If true, loads the Age extension from plugins.</param>
    /// <param name="noInitialization">If true, skips the initialization of the database and graph.</param>
    public AgeDigitalTwinsClient(
        string connectionString,
        string graphName = "digitaltwins",
        bool loadAgeFromPlugins = false
    )
    {
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);
        _dataSource = dataSourceBuilder.UseAge(loadAgeFromPlugins).BuildMultiHost();

        _graphName = graphName;
        _modelParser = new(
            new ParsingOptions()
            {
                MaxDtdlVersion = 4,
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(_graphName, _modelCache, dtmis, ct),
            }
        );
        InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="AgeDigitalTwinsClient"/> instance asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
