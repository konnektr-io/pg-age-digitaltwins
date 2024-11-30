using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DTDLParser;
using Npgsql.Age;
using Npgsql.Age.Types;
using Npgsql;
using DTDLParser.Models;
using AgeDigitalTwins.Validation;
using System.Linq;
using AgeDigitalTwins.Exceptions;
using Json.Patch;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using AgeDigitalTwins.Models;
using System.Text.Json.Nodes;

namespace AgeDigitalTwins;

public class AgeDigitalTwinsClient
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly string _graphName;

    private readonly ModelParser _modelParser;

    public AgeDigitalTwinsClient(NpgsqlDataSource dataSource, string graphName = "digitaltwins")
    {
        _graphName = graphName;
        _dataSource = dataSource;
        _modelParser = new(new ParsingOptions()
        {
            DtmiResolverAsync = (dtmis, ct) => _dataSource.ParserDtmiResolverAsync(_graphName, dtmis, ct)
        });
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public virtual async Task InitializeGraphAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await GraphExistsAsync(cancellationToken) == false)
            {
                Console.WriteLine($"Graph {_graphName} does not exist, creating it now");
                await CreateGraphAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<bool?> GraphExistsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.GraphExistsCommand(_graphName);
            return (bool?)await command.ExecuteScalarAsync(cancellationToken);
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
            await using var command = _dataSource.CreateGraphCommand(_graphName);
            await command.ExecuteNonQueryAsync(cancellationToken);

            // Create labels and indexes
            using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            using var batch = new NpgsqlBatch(connection);
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"SELECT create_vlabel('{_graphName}', 'Twin');"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE UNIQUE INDEX twin_id_idx ON {_graphName}.""Twin"" (ag_catalog.agtype_access_operator(properties, '""$dtId""'::agtype));"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE INDEX twin_gin_idx ON {_graphName}.""Twin"" USING gin (properties);"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"SELECT create_vlabel('{_graphName}', 'Model');"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"SELECT create_elabel('{_graphName}', '_extends');"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE UNIQUE INDEX model_id_idx ON {_graphName}.""Model"" (ag_catalog.agtype_access_operator(properties, '""id""'::agtype));"));
            batch.BatchCommands.Add(new NpgsqlBatchCommand(@$"CREATE INDEX model_gin_idx ON {_graphName}.""Model"" USING gin (properties);"));

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
            Console.WriteLine($"Dropping graph {_graphName}");
            await using var command = _dataSource.DropGraphCommand(_graphName);
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
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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
            var modelData = await GetModelAsync(modelId, cancellationToken);
            var parsedModelEntities = await _modelParser.ParseAsync(modelData.DtdlModel, cancellationToken: cancellationToken);
            var dtInterfaceInfo = (DTInterfaceInfo)parsedModelEntities.FirstOrDefault(e => e.Value is DTInterfaceInfo).Value
                ?? throw new ValidationFailedException($"{modelId} or one of its dependencies does not exist.");
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
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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
        catch (ModelNotFoundException ex)
        {
            // When the model is not found, we should not return a 404, but a 400 as this is an issue with the twin itself
            throw new ValidationFailedException(ex.Message);
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
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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
            string? relationshipJson = relationship is string ? (string)(object)relationship : JsonSerializer.Serialize(relationship);

            // Parse the relationship JSON into a JsonObject
            JsonObject relationshipObject = JsonNode.Parse(relationshipJson)?.AsObject() ?? throw new ArgumentException("Invalid relationship JSON");

            // Check if $relationshipName is present and matches the arguments
            string relationshipName;
            if (relationshipObject.TryGetPropertyValue("$relationshipName", out var relationshipNameNode) && relationshipNameNode is JsonValue relationshipNameValue)
            {
                relationshipName = relationshipNameValue.GetValue<string>() ?? throw new ArgumentException("Relationship's $relationshipName property cannot be null or empty");
            }
            else
            {
                throw new ArgumentException("Relationship's $relationshipName property is missing");
            }
            // Check if $targetId is present and matches the arguments
            string targetId;
            if (relationshipObject.TryGetPropertyValue("$targetId", out var targetIdNode) && targetIdNode is JsonValue targetIdValue)
            {
                targetId = targetIdValue.GetValue<string>() ?? throw new ArgumentException("Relationship's $targetId property cannot be null or empty");
            }
            else
            {
                throw new ArgumentException("Relationship's $targetId property is missing");
            }
            // Check if $sourceId is present and matches the arguments
            if (relationshipObject.TryGetPropertyValue("$sourceId", out var sourceIdNode) && sourceIdNode is JsonValue sourceIdValue)
            {
                string sourceId = sourceIdValue.GetValue<string>() ?? throw new ArgumentException("Relationship's $sourceId property cannot be null or empty");
                if (sourceId != digitalTwinId)
                {
                    throw new ArgumentException("Provided $sourceId does not match the digitalTwinId argument");
                }
            }
            // Check if $relationshipId is present and matches the arguments
            if (relationshipObject.TryGetPropertyValue("$relationshipId", out var relationshipIdNode) && relationshipIdNode is JsonValue relationshipIdValue)
            {
                string relId = relationshipIdValue.GetValue<string>() ?? throw new ArgumentException("Relationship's $relationshipId property cannot be null or empty");
                if (relId != relationshipId)
                {
                    throw new ArgumentException("Provided $relationshipId does not match the relationshipId argument");
                }
            }

            // TODO: Get source and target models and check relationship validity with DTDL parser

            // Ensure $sourceId and $relationshipId are present and correct
            relationshipObject["$sourceId"] = digitalTwinId;
            relationshipObject["$relationshipId"] = relationshipId;

            string updatedRelationshipJson = JsonSerializer.Serialize(relationshipObject);

            string cypher = $@"WITH '{updatedRelationshipJson}'::agtype as relationship
            MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}}),(target:Twin {{`$dtId`: '{targetId}'}})
            MERGE (source)-[rel:{relationshipName} {{`$relationshipId`: '{relationshipId}'}}]->(target)
            SET rel = relationship
            RETURN rel";
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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

    public virtual async IAsyncEnumerable<DigitalTwinsModelData> GetModelsAsync(
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        string cypher = $@"MATCH (m:Model) RETURN m";
        await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
            var vertex = (Vertex)agResult;
            yield return new DigitalTwinsModelData(vertex.Properties);
        }
    }

    public virtual async Task<DigitalTwinsModelData> GetModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string cypher = $@"MATCH (m:Model) WHERE m.id = '{modelId}' RETURN m";
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                return new DigitalTwinsModelData(vertex.Properties);
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

    public virtual async Task<IReadOnlyList<DigitalTwinsModelData>> CreateModelsAsync(
        IEnumerable<string> dtdlModels,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parsedModels = await _modelParser.ParseAsync(ConvertToAsyncEnumerable(dtdlModels), cancellationToken: cancellationToken);
            IEnumerable<DigitalTwinsModelData> modelDatas = dtdlModels.Select(dtdlModel =>
                new DigitalTwinsModelData(dtdlModel));
            // This is needed as after unwinding, it gets converted to agtype again
            string modelsJson = JsonSerializer.Serialize(modelDatas.Select(m => JsonSerializer.Serialize(m)));

            // TODO: do a merge with the id, as we are now just creating new vertices, which isn't the goal
            string cypher = $@"
            UNWIND {modelsJson} as model
            WITH model::agtype as modelAgtype
            CREATE (m:Model)
            SET m = modelAgtype
            RETURN m";

            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Add edges based on the 'extends' field (especially needed for the 'IS_OF_MODEL' function)
            foreach (var model in parsedModels)
            {
                if (model.Value is DTInterfaceInfo dTInterfaceInfo && dTInterfaceInfo.Extends != null && dTInterfaceInfo.Extends.Count > 0)
                {
                    foreach (var extend in dTInterfaceInfo.Extends)
                    {
                        string extendsCypher = $@"MATCH (m:Model), (m2:Model)
                        WHERE m.id = '{dTInterfaceInfo.Id.AbsoluteUri}' AND m2.id = '{extend.Id.AbsoluteUri}'
                        CREATE (m)-[:_extends]->(m2)";
                        await using var extendsCommand = _dataSource.CreateCypherCommand(_graphName, extendsCypher);
                        await extendsCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

            List<DigitalTwinsModelData> result = new List<DigitalTwinsModelData>();
            int k = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                result.Add(new DigitalTwinsModelData(vertex.Properties));
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
            string cypher = $@"
            MATCH (m:Model)
            WHERE m.id = '{modelId}' 
            OPTIONAL MATCH (m)-[r]-()
            DELETE r, m";
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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


    public virtual async IAsyncEnumerable<T?> QueryAsync<T>(
        string query,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {

        NpgsqlDataReader reader;
        try
        {
            string cypher;
            if (query.Contains("SELECT", StringComparison.InvariantCultureIgnoreCase) &&
                !query.Contains("RETURN", StringComparison.InvariantCultureIgnoreCase))
            {
                cypher = AdtQueryHelpers.ConvertAdtQueryToCypher(query);
            }
            else
            {
                cypher = query;
            }
            await using var command = _dataSource.CreateCypherCommand(_graphName, cypher);
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

        var schema = await reader.GetColumnSchemaAsync(cancellationToken);

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
                    string valueString = ((Agtype)value).GetString().Trim('\u0001');
                    if (int.TryParse(valueString, out int intValue))
                    {
                        row.Add(column.ColumnName, intValue);
                    }
                    else if (double.TryParse(valueString, out double doubleValue))
                    {
                        row.Add(column.ColumnName, doubleValue);
                    }
                    else if (bool.TryParse(valueString, out bool boolValue))
                    {
                        row.Add(column.ColumnName, boolValue);
                    }
                    else if (valueString.StartsWith('\"') && valueString.EndsWith('\"'))
                    {
                        row.Add(column.ColumnName, valueString.Trim('\"'));
                    }
                    else if (valueString.StartsWith('[') && valueString.EndsWith(']'))
                    {
                        row.Add(column.ColumnName, ((Agtype)value).GetList());
                    }
                    else if (valueString.StartsWith('{') && valueString.EndsWith('}'))
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(valueString);
                        if (dict != null)
                        {
                            row.Add(column.ColumnName, dict);
                        }
                        else
                        {
                            row.Add(column.ColumnName, valueString);
                        }
                    }
                    else
                    {
                        row.Add(column.ColumnName, valueString);
                    }
                }
            }
            if (typeof(T) == typeof(string))
            {
                yield return (T)(object)JsonSerializer.Serialize(row);
            }
            else
            {
                string json = JsonSerializer.Serialize(row);
                yield return JsonSerializer.Deserialize<T>(json);
            }
        }


    }
}