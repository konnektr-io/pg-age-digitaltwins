using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql.Age;
using Npgsql.Age.Types;
using Npgsql;

namespace AgeDigitalTwins;

public class AgeDigitalTwinsClient : IDisposable
{
    private readonly ILogger<AgeDigitalTwinsClient> _logger;
    private readonly Tracer _tracer;
    private readonly NpgsqlDataSource _dataSource;
    private const string _DEFAULT_GRAPH_NAME = "digitaltwins";

    public AgeDigitalTwinsClient(string connectionString, AgeDigitalTwinsOptions options)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = dataSourceBuilder.UseAge().Build();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dataSource?.Dispose();
    }

    public virtual async Task<T> GetDigitalTwinAsync<T>(
        string digitalTwinId,
        CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("GetDigitalTwinAsync");
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
        {
            // scope.Failed(ex);
            throw;
        }
    }


    public virtual async Task<T> CreateOrReplaceDigitalTwinAsync<T>(
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
}
