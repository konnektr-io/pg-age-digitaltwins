using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.Validation;
using DTDLParser;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient : IAsyncDisposable
{
    private readonly NpgsqlMultiHostDataSource _dataSource;

    private readonly string _graphName;

    private readonly MemoryCache _modelCache = new MemoryCache(new MemoryCacheOptions());

    private readonly MemoryCache _twinCache = new MemoryCache(new MemoryCacheOptions());

    private readonly TimeSpan _modelCacheExpiration;

    private readonly TimeSpan _twinCacheExpiration;

    private readonly ModelParser _modelParser;

    private readonly bool _trackLastUpdatedBy;
    private readonly bool _returnTwinLevelLastUpdatedBy;
    private readonly bool _returnRelationshipMetadata;

    private static readonly ActivitySource ActivitySource = new("AgeDigitalTwins.SDK", "1.0.0");

    /// <summary>
    /// Gets the default batch size for import operations.
    /// </summary>
    public int DefaultBatchSize { get; }

    /// <summary>
    /// Gets the maximum number of items accepted by a single batch
    /// create/replace call. Batches larger than this are rejected with a
    /// <see cref="DigitalTwinBatchLimitExceededException"/> (400).
    /// </summary>
    public int MaxBatchSize { get; }

    /// <summary>
    /// Gets the internal chunk size used to split oversized batch
    /// create/replace calls before they are written to the database.
    /// </summary>
    public int BatchChunkSize { get; }

    /// <summary>
    /// Gets the default checkpoint interval for import operations.
    /// </summary>
    public int DefaultCheckpointInterval { get; }

    /// <summary>
    /// Gets the default heartbeat interval for import operations.
    /// </summary>
    public TimeSpan DefaultHeartbeatInterval { get; }

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
        _twinCacheExpiration = options.TwinCacheExpiration;
        DefaultBatchSize = options.DefaultBatchSize;
        DefaultCheckpointInterval = options.DefaultCheckpointInterval;
        DefaultHeartbeatInterval = options.DefaultHeartbeatInterval;
        MaxBatchSize = options.MaxBatchSize;
        BatchChunkSize = options.BatchChunkSize;
        _trackLastUpdatedBy = options.TrackLastUpdatedBy;
        _returnTwinLevelLastUpdatedBy = options.ReturnTwinLevelLastUpdatedBy;
        _returnRelationshipMetadata = options.ReturnRelationshipMetadata;
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
        _twinCacheExpiration = TimeSpan.FromSeconds(10); // Default to 10 seconds if not set
        DefaultBatchSize = 50; // Default batch size
        DefaultCheckpointInterval = 50; // Default checkpoint interval
        DefaultHeartbeatInterval = TimeSpan.FromSeconds(30); // Default heartbeat interval
        MaxBatchSize = 2000; // Maximum batch create/replace size
        BatchChunkSize = 100; // Internal chunk size for oversized batches
        _returnTwinLevelLastUpdatedBy = true; // Default
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

    /// <summary>
    /// Strips the twin-level <c>$lastUpdatedBy</c> field from a response JSON string
    /// when <see cref="ReturnTwinLevelLastUpdatedBy"/> is <c>false</c>.
    /// Per-property <c>lastUpdatedBy</c> in property metadata is preserved.
    /// Handles nested twins (e.g. in multi-column query results like <c>SELECT Q, R FROM ...</c>)
    /// by recursively traversing the JSON tree.
    /// </summary>
    internal static string StripTwinLevelLastUpdatedBy(string responseJson)
    {
        var node = JsonNode.Parse(responseJson)!;
        StripMetadataLastUpdatedByRecursive(node);
        return node.ToJsonString();
    }

    private static void StripMetadataLastUpdatedByRecursive(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata, out var metaNode)
                && metaNode is JsonObject metaObj)
            {
                metaObj.Remove(DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy);
            }
            foreach (var kvp in obj.ToList())
            {
                if (kvp.Value != null)
                {
                    StripMetadataLastUpdatedByRecursive(kvp.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null)
                {
                    StripMetadataLastUpdatedByRecursive(item);
                }
            }
        }
    }

    /// <summary>
    /// Strips the entire <c>$metadata</c> block from a relationship response JSON.
    /// Relationships in Azure Digital Twins do not have a <c>$metadata</c> block,
    /// so this is removed for ADT compatibility when
    /// <see cref="ReturnRelationshipMetadata"/> is <c>false</c>.
    /// </summary>
    internal static string StripRelationshipMetadata(string responseJson)
    {
        var node = JsonNode.Parse(responseJson)!;
        if (node is JsonObject obj)
        {
            obj.Remove(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata);
        }
        return node.ToJsonString();
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

    /// <summary>
    /// Gets or sets the expiration time for the digital twin existence cache.
    /// </summary>
    public TimeSpan TwinCacheExpiration { get; set; } = TimeSpan.FromSeconds(10); // Default to 10 seconds if not set

    /// <summary>
    /// Gets or sets the default batch size for import operations.
    /// </summary>
    public int DefaultBatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of items accepted by a single batch
    /// create/replace call (<c>CreateOrReplaceDigitalTwinsAsync</c> and
    /// <c>CreateOrReplaceRelationshipsAsync</c>). Batches larger than this
    /// are rejected with a <c>400 Bad Request</c> (problem details stating
    /// the limit); large imports must use an import job. Defaults to
    /// <c>2000</c>.
    /// </summary>
    public int MaxBatchSize { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the internal chunk size used to split an oversized batch
    /// create/replace call into smaller batches before it is written to the
    /// database. The SDK never writes more than this many items in a single
    /// database operation, but still returns an aggregated result for the
    /// whole request. Defaults to <c>100</c>.
    /// </summary>
    public int BatchChunkSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the default checkpoint interval for import operations.
    /// </summary>
    public int DefaultCheckpointInterval { get; set; } = 50;

    /// <summary>
    /// Gets or sets the default heartbeat interval for import operations.
    /// </summary>
    public TimeSpan DefaultHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to track the user ID of the last user who updated
    /// a property in the property metadata. When enabled and a user ID is provided to write
    /// operations, it will be stored as <c>lastUpdatedBy</c> in each updated property's metadata.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool TrackLastUpdatedBy { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether twin-level <c>$lastUpdatedBy</c> is included
    /// in <c>$metadata</c> of response objects. When <c>false</c>, the twin-level
    /// <c>$lastUpdatedBy</c> field is stripped from responses in
    /// <c>GetDigitalTwinAsync</c>, <c>CreateOrReplaceDigitalTwinAsync</c>, and
    /// <c>QueryAsync</c>. Per-property <c>lastUpdatedBy</c> in property metadata is
    /// always preserved. Set to <c>false</c> for compatibility with Azure SDK clients
    /// that cannot deserialize <c>$lastUpdatedBy</c> in <c>$metadata</c>.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool ReturnTwinLevelLastUpdatedBy { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether relationship-level <c>$metadata</c> with
    /// <c>$lastUpdatedBy</c> is included in relationship API responses. When <c>false</c>
    /// (default), the entire <c>$metadata</c> block is stripped from relationship responses
    /// for ADT compatibility — the Azure Digital Twins API does not return <c>$metadata</c>
    /// on relationships. The <c>$lastUpdatedBy</c> value is still stored in the database
    /// and emitted in event/Kusto sinks. Set to <c>true</c> to include relationship
    /// <c>$metadata</c> in API responses.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool ReturnRelationshipMetadata { get; set; } = false;
}
