using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DTDLParser;
using Microsoft.Extensions.Logging;
using Npgsql.Age;
using Npgsql.Age.Types;
using Npgsql;
using OpenTelemetry.Trace;
using DTDLParser.Models;
using AgeDigitalTwins.Validation;
using System.Linq;
using AgeDigitalTwins.Exceptions;

namespace AgeDigitalTwins;

public class AgeDigitalTwinsClient : IDisposable
{
    // private readonly ILogger<AgeDigitalTwinsClient> _logger;
    // private readonly Tracer _tracer;
    private readonly NpgsqlDataSource _dataSource;
    private readonly AgeDigitalTwinsOptions _options;
    private readonly ModelParser _modelParser;

    public AgeDigitalTwinsClient(string connectionString, AgeDigitalTwinsOptions? options)
    {
        // Initialize logger
        // _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        // Initialize data source

        _options = options ?? new();

        NpgsqlConnectionStringBuilder connectionStringBuilder = new(connectionString)
        {
            NoResetOnClose = true
        };

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);

        _dataSource = dataSourceBuilder.UseAge(options?.SuperUser ?? false).Build();

        _modelParser = new(new ParsingOptions()
        {
            DtmiResolverAsync = (dtmis, ct) => _dataSource.ParserDtmiResolverAsync(_options.GraphName, dtmis, ct)
        });
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dataSource?.Dispose();
    }

    public virtual async Task CreateGraphAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.CreateGraphCommand(_options.GraphName);
            await command.ExecuteNonQueryAsync(cancellationToken);

            // TODO: Create indexes
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task DropGraphAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.DropGraphCommand(_options.GraphName);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<T?> GetDigitalTwinAsync<T>(
        string digitalTwinId,
        CancellationToken cancellationToken = default)
    {
        // using var span = _tracer.StartActiveSpan("GetDigitalTwinAsync");
        try
        {
            string cypher = $"MATCH (t:Twin {{ _dtId: '{digitalTwinId}' }}) RETURN t";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
                var vertex = (Vertex)agResult;
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex.Properties));
            }
            else return default;
        }
        catch (Exception ex)
        // scope.Failed(ex);
        {
            throw;
        }
    }

    public virtual async Task<T?> CreateOrReplaceDigitalTwinAsync<T>(
            string digitalTwinId,
            T digitalTwin,
            // ETag? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var propertiesJson = JsonSerializer.Serialize(digitalTwin);
            string cypher = $@"MERGE (t: Twin {{_dtId: '{digitalTwinId}'}})
                            SET t = '{propertiesJson}'::agtype
                            RETURN t";
            string query = $"SELECT * FROM cypher('{_options.GraphName}', $$ {cypher} $$) as (t agtype);";

            await using var command = _dataSource.CreateCommand(query);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex.Properties));
            }
            else return default;
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public static async IAsyncEnumerable<string> ConvertToAsyncEnumerable(IEnumerable<string> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield(); // This is to ensure the method is truly asynchronous
        }
    }

    public virtual async Task<string> GetModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string cypher = $@"MATCH (m:Model) WHERE m['@id'] = '{modelId}' RETURN m";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                return JsonSerializer.Serialize(vertex.Properties);
            }
            else
            {
                throw new ModelNotFoundException($"Model with ID {modelId} not found");
            }
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public virtual async Task<string[]> CreateModelsAsync(
        IEnumerable<string> dtdlModels,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var asyncModels = ConvertToAsyncEnumerable(dtdlModels);
            var parsedModels = await _modelParser.ParseAsync(asyncModels, cancellationToken: cancellationToken);
            var modelsJson = JsonSerializer.Serialize(dtdlModels);
            string cypher = $@"
            UNWIND {modelsJson} as model
            WITH model::agtype as modelAgtype
            CREATE (m:Model)
            SET m = modelAgtype
            RETURN m";

            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            string[] result = new string[dtdlModels.Count()];
            int k = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                result[k] = JsonSerializer.Serialize(vertex.Properties);
                k++;
            }
            return result;
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public virtual async Task DeleteModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string cypher = $@"MATCH (m:Model) WHERE m['@id'] = '{modelId}' DELETE m";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected == 0)
            {
                throw new ModelNotFoundException($"Model with ID {modelId} not found");
            }
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }
}