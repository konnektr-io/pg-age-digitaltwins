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

namespace AgeDigitalTwins;

public class AgeDigitalTwinsClient : IDisposable
{
    // private readonly ILogger<AgeDigitalTwinsClient> _logger;
    // private readonly Tracer _tracer;
    private readonly NpgsqlDataSource _dataSource;
    private const string _DEFAULT_GRAPH_NAME = "digitaltwins";
    private readonly ModelParser _modelParser;

    public AgeDigitalTwinsClient(string connectionString, AgeDigitalTwinsOptions options)
    {
        // Initialize logger
        // _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        // Initialize data source
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = dataSourceBuilder.UseAge(options.SuperUser).Build();
        _modelParser = new(new ParsingOptions()
        {
            DtmiResolverAsync = (dtmis, ct) => _dataSource.ParserDtmiResolverAsync(_DEFAULT_GRAPH_NAME, dtmis, ct)
        });
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dataSource?.Dispose();
    }

    public virtual async Task CreateGraphAsync(
        string graphName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.CreateGraphCommand(graphName);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task DropGraphAsync(
        string graphName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.DropGraphCommand(graphName);
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
            await using var command = _dataSource.CreateCypherCommand(_DEFAULT_GRAPH_NAME, cypher);
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
            string query = $"SELECT * FROM cypher('{_DEFAULT_GRAPH_NAME}', $$ {cypher} $$) as (t agtype);";

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

    public virtual async Task<JsonElement> CreateModelsAsync(
        IEnumerable<string> dtdlModels,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var asyncModels = ConvertToAsyncEnumerable(dtdlModels);
            var parsedModels = await _modelParser.ParseAsync(asyncModels, cancellationToken: cancellationToken);

            string cypher = $"CREATE ";
            string cypherReturnPart = "\nRETURN ";
            string returnPart = "";
            var i = 0;
            foreach (var model in dtdlModels)
            {
                cypher += $"(m_{i}:Model '{model}'::agtype),";
                cypherReturnPart += $"m_{i} agtype,";
                returnPart += $"m_{i} agtype,";
                i++;
            }
            cypher = cypher.TrimEnd(',');
            cypherReturnPart = cypherReturnPart.TrimEnd(',');
            cypher += cypherReturnPart;
            returnPart = returnPart.TrimEnd(',');
            string query = $"SELECT * FROM cypher('{_DEFAULT_GRAPH_NAME}', $$ {cypher} $$) as ({returnPart});";

            await using var command = _dataSource.CreateCommand(query);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(vertex.Properties));
            }
            else return default;
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    /*     public virtual async Task<DTInterfaceInfo> GetModelAsync(
                string modelId,
                CancellationToken cancellationToken = default)
        {
            try
            {
                var propertiesJson = JsonSerializer.Serialize(digitalTwin);
                string cypher = $@"MERGE (t: Twin {{_dtId: '{digitalTwinId}'}})
                                SET t = '{propertiesJson}'::agtype
                                RETURN t";
                string query = $"SELECT * FROM cypher('{_DEFAULT_GRAPH_NAME}', $$ {cypher} $$) as (t agtype);";

                await using var command = _dataSource.CreateCommand(query);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                    var vertex = (Vertex)agResult;
                    return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(vertex.Properties));
                }
                else return default;
            }
            catch (Exception ex)
            {
                // scope.Failed(ex);
                throw;
            }
        } */
}