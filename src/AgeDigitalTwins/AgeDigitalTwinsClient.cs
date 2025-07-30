using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Validation;
using DTDLParser;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
    /// Gets the default batch size for import operations.
    /// </summary>
    public int DefaultBatchSize { get; }

    /// <summary>
    /// Gets the default checkpoint interval for import operations.
    /// </summary>
    public int DefaultCheckpointInterval { get; }

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
        DefaultBatchSize = options.DefaultBatchSize;
        DefaultCheckpointInterval = options.DefaultCheckpointInterval;
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
        JobService = new JobService(_dataSource, _graphName);
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
        DefaultBatchSize = 50; // Default batch size
        DefaultCheckpointInterval = 50; // Default checkpoint interval
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
        JobService = new JobService(_dataSource, _graphName);
        InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the data source for connecting to the database.
    /// </summary>
    /// <returns>The NpgsqlMultiHostDataSource instance used by this client.</returns>
    public NpgsqlMultiHostDataSource GetDataSource()
    {
        return _dataSource;
    }

    /// <summary>
    /// Gets the graph name used by this client.
    /// </summary>
    /// <returns>The graph name.</returns>
    public string GetGraphName()
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

    /// <summary>
    /// Gets the job service for managing import and other jobs.
    /// </summary>
    public JobService JobService { get; }
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

    /// <summary>
    /// Gets or sets the default batch size for import operations.
    /// </summary>
    public int DefaultBatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the default checkpoint interval for import operations.
    /// </summary>
    public int DefaultCheckpointInterval { get; set; } = 50;
}
