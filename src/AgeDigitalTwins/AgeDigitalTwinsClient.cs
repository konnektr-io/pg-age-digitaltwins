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

    private readonly TimeSpan _modelCacheExpiration;

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
        AgeDigitalTwinsClientOptions? options = null
    )
    {
        _dataSource = dataSource;
        options ??= new AgeDigitalTwinsClientOptions();
        _graphName = options.GraphName;
        _modelCacheExpiration = options.ModelCacheExpiration;
        _modelParser = new(
            new ParsingOptions()
            {
                MaxDtdlVersion = 4,
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(
                        _graphName,
                        _modelCache,
                        _modelCacheExpiration,
                        dtmis,
                        ct
                    ),
            }
        );
        InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeDigitalTwinsClient"/> class with a data source and graph name.
    /// </summary>
    /// <param name="dataSource">The data source for connecting to the database.</param>
    /// <param name="graphName">The name of the graph to use. Defaults to "digitaltwins".</param>
    /// <param name="noInitialization">If true, skips the initialization of the database and graph.</param>
    public AgeDigitalTwinsClient(NpgsqlMultiHostDataSource dataSource, string graphName)
    {
        _dataSource = dataSource;
        _graphName = graphName;
        _modelCacheExpiration = TimeSpan.FromSeconds(10); // Default to 10 seconds if not set
        _modelParser = new(
            new ParsingOptions()
            {
                MaxDtdlVersion = 4,
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(
                        _graphName,
                        _modelCache,
                        _modelCacheExpiration,
                        dtmis,
                        ct
                    ),
            }
        );
        InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the data source for connecting to the database.
    /// </summary>
    /// <returns>The NpgsqlMultiHostDataSource instance used by this client.</returns>
    internal NpgsqlMultiHostDataSource GetDataSource()
    {
        return _dataSource;
    }

    /// <summary>
    /// Gets the graph name used by this client.
    /// </summary>
    /// <returns>The graph name.</returns>
    internal string GetGraphName()
    {
        return _graphName;
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

public class AgeDigitalTwinsClientOptions
{
    /// <summary>
    /// Gets or sets the name of the graph to use.
    /// </summary>
    public string GraphName { get; set; } = "digitaltwins";

    /// <summary>
    /// Gets or sets the expiration time for the model cache.
    /// </summary>
    public TimeSpan ModelCacheExpiration { get; set; } = TimeSpan.FromSeconds(10); // Default to 10 seconds if not set
}
