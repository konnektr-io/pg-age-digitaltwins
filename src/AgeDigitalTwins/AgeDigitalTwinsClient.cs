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
using Json.Patch;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace AgeDigitalTwins;

public class AgeDigitalTwinsClient : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly AgeDigitalTwinsOptions _options;
    private readonly ModelParser _modelParser;

    public AgeDigitalTwinsClient(string connectionString, AgeDigitalTwinsOptions? options)
    {
        _options = options ?? new();

        NpgsqlConnectionStringBuilder connectionStringBuilder = new(connectionString)
        {
            NoResetOnClose = true
        };

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);

        _dataSource = dataSourceBuilder
            .UseLoggerFactory(options?.LoggerFactory)
            .UseAge(options?.SuperUser ?? false)
            .Build();

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

    public virtual async Task<bool?> GraphExistsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.GraphExistsCommand(_options.GraphName);
            return (bool)await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task CreateGraphAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.CreateGraphCommand(_options.GraphName);
            await command.ExecuteNonQueryAsync(cancellationToken);

            // Create labels and indexes
            using var connection = await _dataSource.OpenConnectionAsync();
            using var batch = new NpgsqlBatch(connection);
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"SELECT create_vlabel('{_options.GraphName}', 'Twin');"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE UNIQUE INDEX twin_id_idx ON {_options.GraphName}.""Twin"" (ag_catalog.agtype_access_operator(properties, '""$dtId""'::agtype));"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE INDEX twin_gin_idx ON {_options.GraphName}.""Twin"" USING gin (properties);"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"SELECT create_vlabel('{_options.GraphName}', 'Model');"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE UNIQUE INDEX model_id_idx ON {_options.GraphName}.""Model"" (ag_catalog.agtype_access_operator(properties, '""@id""'::agtype));"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE INDEX model_gin_idx ON {_options.GraphName}.""Model"" USING gin (properties);"));
            await batch.ExecuteNonQueryAsync(cancellationToken);
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

    public virtual async Task<T> GetDigitalTwinAsync<T>(
        string digitalTwinId,
        CancellationToken cancellationToken = default)
    {
        // using var span = _tracer.StartActiveSpan("GetDigitalTwinAsync");
        try
        {
            string cypher = $"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}' RETURN t";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
                var vertex = (Vertex)agResult;
                var twin = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex.Properties))
                    ?? throw new SerializationException($"Digital Twin with ID {digitalTwinId} could not be deserialized");
                return twin;
            }
            else
            {
                throw new DigitalTwinNotFoundException($"Digital Twin with ID {digitalTwinId} not found");
            }
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
            var digitalTwinJson = digitalTwin is string ? (string)(object)digitalTwin : JsonSerializer.Serialize(digitalTwin);

            using var digitalTwinDocument = JsonDocument.Parse(digitalTwinJson);
            if (!digitalTwinDocument.RootElement.TryGetProperty("$metadata", out var metadataElement) || metadataElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Digital Twin must have a $metadata property of type object");
            }
            if (!metadataElement.TryGetProperty("$model", out var modelElement) || modelElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Digital Twin's $metadata must contain a $model property of type string");
            }

            string modelId = modelElement.GetString() ?? throw new ArgumentException("Digital Twin's $model property cannot be null or empty");

            // Get the model and parse it
            var modelJson = await GetModelAsync(modelId, cancellationToken);
            var parsedModelEntities = await _modelParser.ParseAsync(modelJson, cancellationToken: cancellationToken);
            var dtInterfaceInfo = (DTInterfaceInfo)parsedModelEntities.FirstOrDefault(e => e.Value is DTInterfaceInfo).Value;

            if (dtInterfaceInfo == null)
            {
                throw new ModelNotFoundException($"Model with ID {modelId} not found");
            }

            List<string> violations = new();

            foreach (var kv in digitalTwinDocument.RootElement.EnumerateObject())
            {
                var property = kv.Name;
                var value = kv.Value;

                if (property == "$metadata" || property == "$dtId" || property == "$etag")
                {
                    continue;
                }

                if (!dtInterfaceInfo.Contents.TryGetValue(property, out DTContentInfo? contentInfo))
                {
                    violations.Add($"Property '{property}' is not defined in the model");
                    continue;
                }

                if (contentInfo is DTPropertyInfo propertyDef)
                {
                    violations.AddRange(propertyDef.Schema.ValidateInstance(value).Select(v => $"Property '{property}': {v}"));
                }
                else
                {
                    violations.Add($"Property '{property}' is a {contentInfo.GetType()} and is not supported");
                }
            }

            if (violations.Count != 0)
            {
                throw new ValidationFailedException(string.Join(" AND ", violations));
            }

            string cypher = $@"
            WITH '{digitalTwinJson}'::agtype as twin
            MERGE (t: Twin {{`$dtId`: '{digitalTwinId}'}})
            SET t = twin
            RETURN t";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)JsonSerializer.Serialize(vertex.Properties);
                }
                else
                {
                    return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex.Properties));
                }
            }
            else return default;
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public virtual async Task UpdateDigitalTwinAsync(
        string digitalTwinId,
        JsonPatch patch,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<string> violations = new();

            var cypherOperations = new List<string>();
            foreach (var op in patch.Operations)
            {
                var path = op.Path.ToString().TrimStart('/').Replace("/", ".");
                if (path == "$dtId")
                {
                    violations.Add("Cannot update the $dtId property");
                }
                if (op.Op == OperationType.Add || op.Op == OperationType.Replace)
                {
                    cypherOperations.Add($"SET t.{path} = {op.Value}");

                }
                else if (op.Op == OperationType.Remove)
                {
                    cypherOperations.Add($"REMOVE t.{path}");
                }
                else
                {
                    throw new NotSupportedException($"Operation '{op.Op}' is not supported");
                }
            }

            string cypher = $@"
            MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}'
            {string.Join("\n", cypherOperations)}
            RETURN t";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new DigitalTwinNotFoundException($"Digital Twin with ID {digitalTwinId} not found");
            }
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public virtual async Task DeleteDigitalTwinAsync(
        string digitalTwinId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string cypher = $@"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}' DELETE t";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected == 0)
            {
                throw new DigitalTwinNotFoundException($"Digital Twin with ID {digitalTwinId} not found");
            }
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public virtual async Task<T?> GetRelationshipAsync<T>(
        string digitalTwinId,
        string relationshipId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string cypher = $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel {{`$relationshipId`: '{relationshipId}'}}]->(target:Twin) RETURN rel";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var edge = (Edge)agResult;
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(edge.Properties));
            }
            else
            {
                throw new DigitalTwinNotFoundException($"Relationship with ID {relationshipId} not found");
            }
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public virtual async IAsyncEnumerable<T?> GetRelationshipsAsync<T>(
        string digitalTwinId,
        string? relationshipName = default,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        string edgeLabel = !string.IsNullOrEmpty(relationshipName) ? "" : $":{relationshipName}";
        string cypher = $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel{edgeLabel}]->(target:Twin) RETURN rel";
        await foreach (JsonElement json in QueryAsync<JsonElement>(cypher, cancellationToken))
        {
            yield return JsonSerializer.Deserialize<T>(json.GetProperty("rel").GetRawText());
        }
    }


    public virtual async IAsyncEnumerable<T?> GetIncomingRelationshipsAsync<T>(
        string digitalTwinId,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {

        string cypher = $@"MATCH (source:Twin)-[rel]->(target:Twin {{`$dtId`: '{digitalTwinId}'}}) RETURN rel";
        await foreach (JsonElement json in QueryAsync<JsonElement>(cypher, cancellationToken))
        {
            yield return JsonSerializer.Deserialize<T>(json.GetProperty("rel").GetRawText());
        }
    }

    public virtual async Task<T?> CreateOrReplaceRelationshipAsync<T>(
        string digitalTwinId,
        string relationshipId,
        T relationship,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var relationshipJson = relationship is string ? (string)(object)relationship : JsonSerializer.Serialize(relationship);

            using var relationshipDocument = JsonDocument.Parse(relationshipJson);
            if (!relationshipDocument.RootElement.TryGetProperty("$relationshipName", out var relationshipNameElement) || relationshipNameElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Relationship must contain a $relationshipName property of type string");
            }
            if (!relationshipDocument.RootElement.TryGetProperty("$targetId", out var targetIdElement) || targetIdElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Relationship must contain a $targetId property of type string");
            }

            string relationshipName = relationshipNameElement.GetString() ?? throw new ArgumentException("Relationship's $relationshipName property cannot be null or empty");

            // TODO: use merge to fix this
            // Make sure there's only a single relationshipid for each source digital twin

            string cypher = $@"WITH '{relationshipJson}'::agtype as relationship
            MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}}),(target:Twin {{`$dtId`: '{targetIdElement.GetString()}'}})
            MERGE (source)-[rel:{relationshipName} {{`$relationshipId`: '{relationshipId}'}}]->(target)
            SET rel = relationship
            RETURN rel";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var edge = (Edge)agResult;

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)JsonSerializer.Serialize(edge.Properties);
                }
                else
                {
                    return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(edge.Properties));
                }
            }
            else return default;
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }
    }

    public virtual async Task DeleteRelationshipAsync(
        string digitalTwinId,
        string relationshipId,
        string relationshipName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string cypher = $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel:{relationshipName} {{`$relationshipId`: '{relationshipId}'}}]->(target:Twin) DELETE rel";
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected == 0)
            {
                throw new DigitalTwinNotFoundException($"Relationship with ID {relationshipId} not found");
            }
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

            // Add edges based on the 'extends' field (especially needed for the 'IS_OF_MODEL' function)
            foreach (var model in parsedModels)
            {
                if (model.Value is DTInterfaceInfo dTInterfaceInfo && dTInterfaceInfo.Extends != null && dTInterfaceInfo.Extends.Count > 0)
                {
                    foreach (var extend in dTInterfaceInfo.Extends)
                    {
                        string extendsCypher = $@"MATCH (m:Model), (m2:Model)
                        WHERE m['@id'] = '{dTInterfaceInfo.Id.AbsoluteUri}' AND m2['@id'] = '{extend.Id.AbsoluteUri}'
                        CREATE (m)-[:_extends]->(m2)";
                        await using var extendsCommand = _dataSource.CreateCypherCommand(_options.GraphName, extendsCypher);
                        await extendsCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

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

    internal static string ConvertAdtQueryToCypher(string adtQuery)
    {
        if (adtQuery.ToUpperInvariant().Contains("FROM RELATIONSHIPS"))
        {
            if (adtQuery.ToUpperInvariant().Contains("WHERE"))
            {
                // Handle RELATIONSHIPS source
                var match = Regex.Match(adtQuery, @"SELECT (\w+) FROM RELATIONSHIPS \1 WHERE \1\.\$(\w+) = '(\w+)'");
                if (match.Success)
                {
                    var relationship = match.Groups[1].Value;
                    var sourceId = match.Groups[2].Value;
                    var value = match.Groups[3].Value;
                    return $"MATCH (:Twin)-[{relationship}]->(:Twin) WHERE {relationship}['${sourceId}'] = '{value}' RETURN {relationship}";
                }
            }
            else
            {
                // Handle case with no WHERE clause
                var match = Regex.Match(adtQuery, @"SELECT (\w+) FROM RELATIONSHIPS \1");
                if (match.Success)
                {
                    var relationship = match.Groups[1].Value;
                    return $"MATCH (:Twin)-[{relationship}]->(:Twin) RETURN {relationship}";
                }
            }

        }
        else if (adtQuery.ToUpperInvariant().Contains("FROM DIGITALTWINS"))
        {
            // Handle DIGITALTWINS source
            if (adtQuery.ToUpperInvariant().Contains("MATCH"))
            {
                // Handle MATCH clause
                var match = Regex.Match(adtQuery, @"SELECT (.+) FROM DIGITALTWINS MATCH (.+) WHERE (.+)");
                if (match.Success)
                {
                    var select = match.Groups[1].Value;
                    var matchClause = match.Groups[2].Value;
                    var whereClause = match.Groups[3].Value;

                    // Add :Twin to all round brackets in the MATCH clause
                    var modifiedMatchClause = Regex.Replace(matchClause, @"\((\w+)\)", "($1:Twin)");

                    // Process WHERE clause
                    var modifiedWhereClause = ProcessWhereClause(whereClause);

                    return $"MATCH {modifiedMatchClause} WHERE {modifiedWhereClause} RETURN {select}";
                }
            }
            else if (adtQuery.ToUpperInvariant().Contains("WHERE"))
            {
                // Handle WHERE clause
                var match = Regex.Match(adtQuery, @"SELECT (\w+) FROM DIGITALTWINS \1 WHERE (.+)");
                if (match.Success)
                {
                    var twin = match.Groups[1].Value;
                    var whereClause = match.Groups[2].Value;

                    // Process WHERE clause
                    var modifiedWhereClause = ProcessWhereClause(whereClause);

                    return $"MATCH ({twin}:Twin) WHERE {modifiedWhereClause} RETURN {twin}";
                }
            }
            else
            {
                // Handle case with no WHERE clause
                var match = Regex.Match(adtQuery, @"SELECT (\w+) FROM DIGITALTWINS \1");
                if (match.Success)
                {
                    var twin = match.Groups[1].Value;
                    return $"MATCH ({twin}:Twin) RETURN {twin}";
                }
            }
        }

        return "Invalid query format.";
    }

    private static string ProcessWhereClause(string whereClause)
    {
        // Replace property access with $ character
        return Regex.Replace(whereClause, @"(\.\$[\w]+)", m => $"['{m.Value.Substring(2)}']");
    }

    public virtual async IAsyncEnumerable<T?> QueryAsync<T>(
        string query,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {

        NpgsqlDataReader reader;
        try
        {
            string cypher;
            if (query.ToUpperInvariant().Contains("SELECT") && !query.ToUpperInvariant().Contains("RETURN"))
            {
                cypher = ConvertAdtQueryToCypher(query);
            }
            else
            {
                cypher = query;
            }
            await using var command = _dataSource.CreateCypherCommand(_options.GraphName, cypher);
            reader = await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // scope.Failed(ex);
            throw;
        }

        if (reader == null)
        {
            throw new InvalidOperationException("Reader is null");
        }

        var schema = await reader.GetColumnSchemaAsync();

        while (await reader.ReadAsync(cancellationToken))
        {
            Dictionary<string, object> row = new();
            // iterate over columns
            for (int i = 0; i < schema.Count; i++)
            {
                var column = schema[i];
                var value = await reader.GetFieldValueAsync<Agtype?>(i);
                if (value == null)
                {
                    continue;
                }
                if (((Agtype)value).IsVertex)
                {
                    row.Add(column.ColumnName, ((Vertex)value).Properties);
                }
                else if (((Agtype)value).IsEdge)
                {
                    row.Add(column.ColumnName, ((Edge)value).Properties);
                }
                else
                {
                    row.Add(column.ColumnName, value);
                }
            }
            if (typeof(T) == typeof(string))
            {
                yield return (T)(object)JsonSerializer.Serialize(row);
            }
            else
            {
                yield return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(row));
            }
        }


    }
}