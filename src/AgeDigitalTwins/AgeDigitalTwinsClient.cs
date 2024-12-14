using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.Validation;
using DTDLParser;
using DTDLParser.Models;
using Json.More;
using Json.Patch;
using Npgsql;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins;

public class AgeDigitalTwinsClient : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly string _graphName;

    private readonly ModelParser _modelParser;

    private readonly JsonSerializerOptions serializerOptions =
        new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public AgeDigitalTwinsClient(NpgsqlDataSource dataSource, string graphName = "digitaltwins")
    {
        _graphName = graphName;
        _dataSource = dataSource;
        _modelParser = new(
            new ParsingOptions()
            {
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public AgeDigitalTwinsClient(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string graphName = "digitaltwins"
    )
    {
        connectionStringBuilder.SearchPath = "ag_catalog, \"$user\", public";
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);
        _dataSource = dataSourceBuilder.UseAge(false).Build();

        _graphName = graphName;
        _modelParser = new(
            new ParsingOptions()
            {
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public AgeDigitalTwinsClient(string connectionString, string graphName = "digitaltwins")
    {
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);
        _dataSource = dataSourceBuilder.UseAge(false).Build();

        _graphName = graphName;
        _modelParser = new(
            new ParsingOptions()
            {
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    #region Graph

    public virtual async Task InitializeGraphAsync(CancellationToken cancellationToken = default)
    {
        if (await GraphExistsAsync(cancellationToken) != true)
        {
            await CreateGraphAsync(cancellationToken);
        }
    }

    public virtual async Task<bool?> GraphExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.GraphExistsCommand(_graphName);
        return (bool?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public virtual async Task CreateGraphAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateGraphCommand(_graphName);
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Initialize the graph by creating labels, indexes, functions, ...
        using var batch = new NpgsqlBatch(connection);
        foreach (
            NpgsqlBatchCommand initBatchCommand in GraphInitialization.GetGraphInitCommands(
                _graphName
            )
        )
        {
            batch.BatchCommands.Add(initBatchCommand);
        }
        await batch.ExecuteNonQueryAsync(cancellationToken);
    }

    public virtual async Task DropGraphAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.DropGraphCommand(_graphName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Digital Twins

    public virtual async Task<bool> DigitalTwinExistsAsync(
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher = $"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}' RETURN t";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    public virtual async Task<bool> DigitalTwinEtagMatchesAsync(
        string digitalTwinId,
        string etag,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}' AND t['$etag'] = '{etag}' RETURN t";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    public virtual async Task<T> GetDigitalTwinAsync<T>(
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher = $"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}' RETURN t";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
            var vertex = (Vertex)agResult;
            var twin =
                JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex.Properties))
                ?? throw new SerializationException(
                    $"Digital Twin with ID {digitalTwinId} could not be deserialized"
                );
            return twin;
        }
        else
        {
            throw new DigitalTwinNotFoundException(
                $"Digital Twin with ID {digitalTwinId} not found"
            );
        }
    }

    public virtual async Task<T?> CreateOrReplaceDigitalTwinAsync<T>(
        string digitalTwinId,
        T digitalTwin,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            DateTime now = DateTime.UtcNow;

            string digitalTwinJson =
                digitalTwin is string
                    ? (string)(object)digitalTwin
                    : JsonSerializer.Serialize(digitalTwin);

            JsonObject digitalTwinObject =
                JsonNode.Parse(digitalTwinJson)?.AsObject()
                ?? throw new ArgumentException("Invalid digital twin JSON");
            if (
                !digitalTwinObject.TryGetPropertyValue("$metadata", out JsonNode? metadataNode)
                || metadataNode is not JsonObject metadataObject
            )
            {
                throw new ArgumentException(
                    "Digital Twin must have a $metadata property of type object"
                );
            }
            if (
                !metadataObject.TryGetPropertyValue("$model", out JsonNode? modelNode)
                || modelNode is not JsonValue modelValue
                || modelValue.GetValueKind() != JsonValueKind.String
            )
            {
                throw new ArgumentException(
                    "Digital Twin's $metadata must contain a $model property of type string"
                );
            }
            if (
                digitalTwinObject.TryGetPropertyValue("$dtId", out JsonNode? dtIdNode)
                && dtIdNode is JsonValue dtIdValue
                && digitalTwinId != dtIdValue.ToString()
            )
            {
                throw new ArgumentException(
                    "Provided digitalTwinId does not match the $dtId property"
                );
            }
            if (!string.IsNullOrEmpty(ifNoneMatch) && !ifNoneMatch.Equals("*"))
            {
                throw new ArgumentException(
                    "Invalid If-None-Match header value. Allowed value(s): If-None-Match: *"
                );
            }

            if (ifNoneMatch == "*")
            {
                if (await DigitalTwinExistsAsync(digitalTwinId, cancellationToken))
                {
                    throw new PreconditionFailedException(
                        $"If-None-Match: * header was specified but a twin with the id {digitalTwinId} was found. Please specify a different twin id."
                    );
                }
            }

            string modelId =
                modelValue.ToString()
                ?? throw new ArgumentException(
                    "Digital Twin's $model property cannot be null or empty"
                );

            // Get the model and parse it
            DigitalTwinsModelData modelData = await GetModelAsync(modelId, cancellationToken);
            IReadOnlyDictionary<Dtmi, DTEntityInfo> parsedModelEntities =
                await _modelParser.ParseAsync(
                    modelData.DtdlModel,
                    cancellationToken: cancellationToken
                );
            DTInterfaceInfo dtInterfaceInfo =
                (DTInterfaceInfo)
                    parsedModelEntities.FirstOrDefault(e => e.Value is DTInterfaceInfo).Value
                ?? throw new ValidationFailedException(
                    $"{modelId} or one of its dependencies does not exist."
                );
            List<string> violations = new();

            foreach (KeyValuePair<string, JsonNode?> kv in digitalTwinObject)
            {
                string property = kv.Key;
                JsonElement value = kv.Value.ToJsonDocument().RootElement;

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
                    IReadOnlyCollection<string> validationFailures =
                        propertyDef.Schema.ValidateInstance(value);
                    if (validationFailures.Count != 0)
                    {
                        violations.AddRange(
                            validationFailures.Select(v => $"Property '{property}': {v}")
                        );
                    }
                    else
                    {
                        // Set last update time
                        if (
                            metadataObject.TryGetPropertyValue(
                                property,
                                out JsonNode? metadataPropertyNode
                            ) && metadataPropertyNode is JsonObject metadataPropertyObject
                        )
                        {
                            metadataPropertyObject["lastUpdateTime"] = now.ToString("o");
                        }
                        else
                        {
                            metadataObject[property] = new JsonObject
                            {
                                ["lastUpdateTime"] = now.ToString("o"),
                            };
                        }
                    }
                }
                else
                {
                    violations.Add(
                        $"Property '{property}' is a {contentInfo.GetType()} and is not supported"
                    );
                }
            }

            if (violations.Count != 0)
            {
                throw new ValidationFailedException(string.Join(" AND ", violations));
            }

            // Set global last update time
            metadataObject["$lastUpdateTime"] = now.ToString("o");
            // Set new etag
            string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
            digitalTwinObject["$etag"] = newEtag;

            // Serialize the updated digital twin
            string updatedDigitalTwinJson = JsonSerializer
                .Serialize(digitalTwinObject, serializerOptions)
                .Replace("'", "\\'");

            string cypher =
                $@"WITH '{updatedDigitalTwinJson}'::agtype as twin
            MERGE (t: Twin {{`$dtId`: '{digitalTwinId}'}})
            SET t = twin
            RETURN t";
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCypherCommand(_graphName, cypher);
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
                    return JsonSerializer.Deserialize<T>(
                        JsonSerializer.Serialize(vertex.Properties)
                    );
                }
            }
            else
                return default;
        }
        catch (ModelNotFoundException ex)
        {
            // When the model is not found, we should not return a 404, but a 400 as this is an issue with the twin itself
            throw new ValidationFailedException(ex.Message);
        }
    }

    public virtual async Task UpdateDigitalTwinAsync(
        string digitalTwinId,
        JsonPatch patch,
        string? ifMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        DateTime now = DateTime.UtcNow;

        // Check etag if defined
        if (!string.IsNullOrEmpty(ifMatch) && !ifMatch.Equals("*"))
        {
            if (!await DigitalTwinEtagMatchesAsync(digitalTwinId, ifMatch, cancellationToken))
            {
                throw new PreconditionFailedException(
                    $"If-Match: {ifMatch} header value does not match the current ETag value of the digital twin with id {digitalTwinId}"
                );
            }
        }

        List<string> violations = new();

        List<string> updateTimeSetOperations =
            new()
            {
                $"SET t = public.agtype_set(properties(t),['$metadata','$lastUpdateTime'],'{now:o}')",
            };
        List<string> patchOperations = new();

        foreach (var op in patch.Operations)
        {
            var path = op.Path.ToString().TrimStart('/').Replace("/", ".");
            if (path == "$dtId")
            {
                violations.Add("Cannot update the $dtId property");
            }
            if (op.Value != null && (op.Op == OperationType.Add || op.Op == OperationType.Replace))
            {
                if (
                    op.Value.GetValueKind() == JsonValueKind.Object
                    || op.Value.GetValueKind() == JsonValueKind.Array
                )
                {
                    patchOperations.Add(
                        $"SET t = public.agtype_set(properties(t),['{string.Join("','", path.Split('.'))}'],'{JsonSerializer.Serialize(op.Value, serializerOptions)}')"
                    );
                }
                else if (op.Value.GetValueKind() == JsonValueKind.String)
                {
                    patchOperations.Add(
                        $"SET t = public.agtype_set(properties(t),['{string.Join("','", path.Split('.'))}'],'{op.Value}')"
                    );
                }
                else
                {
                    patchOperations.Add(
                        $"SET t = public.agtype_set(properties(t),['{string.Join("','", path.Split('.'))}'],{op.Value})"
                    );
                }
                // UpdateTime is set on the root of the property
                updateTimeSetOperations.Add(
                    $"SET t = public.agtype_set(properties(t),['$metadata','{path.Split('.').First()}','lastUpdateTime'],'{now:o}')"
                );
            }
            else if (op.Op == OperationType.Remove)
            {
                patchOperations.Add(
                    $"SET t = public.agtype_delete_key(properties(t),['{string.Join("','", path.Split('.'))}'])"
                );
                // This won't do anything for nested properties (which is fine as we need to keep the root property last update time)
                updateTimeSetOperations.Add(
                    $"SET t = public.agtype_set(properties(t),['$metadata','{string.Join("','", path.Split('.'))}','lastUpdateTime'],'{now:o}')"
                );
            }
            else
            {
                throw new NotSupportedException(
                    $"Operation '{op.Op}' with value '{op.Value}' is not supported"
                );
            }
        }

        string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
        patchOperations.Add($"SET t.`$etag` = '{newEtag}'");

        string cypher =
            $@"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}'
            {string.Join("\n", updateTimeSetOperations)}
            {string.Join("\n", patchOperations)}
            RETURN t";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new DigitalTwinNotFoundException(
                $"Digital Twin with ID {digitalTwinId} not found"
            );
        }
    }

    public virtual async Task DeleteDigitalTwinAsync(
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher = $@"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId}' DELETE t";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            throw new DigitalTwinNotFoundException(
                $"Digital Twin with ID {digitalTwinId} not found"
            );
        }
    }

    #endregion

    #region Relationships

    public virtual async Task<bool> RelationshipExistsAsync(
        string digitalTwinId,
        string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel {{`$relationshipId`: '{relationshipId}'}}]->(target:Twin) RETURN rel";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    public virtual async Task<bool> RelationshipEtagMatchesAsync(
        string digitalTwinId,
        string relationshipId,
        string etag,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel {{`$relationshipId`: '{relationshipId}'}}]->(target:Twin) WHERE rel['$etag'] = '{etag}' RETURN rel";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    public virtual async Task<T?> GetRelationshipAsync<T>(
        string digitalTwinId,
        string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel {{`$relationshipId`: '{relationshipId}'}}]->(target:Twin) RETURN rel";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
            var edge = (Edge)agResult;
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(edge.Properties));
        }
        else
        {
            throw new DigitalTwinNotFoundException(
                $"Relationship with ID {relationshipId} not found"
            );
        }
    }

    public virtual async IAsyncEnumerable<T?> GetRelationshipsAsync<T>(
        string digitalTwinId,
        string? relationshipName = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        string edgeLabel = !string.IsNullOrEmpty(relationshipName) ? "" : $":{relationshipName}";
        string cypher =
            $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel{edgeLabel}]->(target:Twin) RETURN rel";
        await foreach (JsonElement json in QueryAsync<JsonElement>(cypher, cancellationToken))
        {
            yield return JsonSerializer.Deserialize<T>(json.GetProperty("rel").GetRawText());
        }
    }

    public virtual async IAsyncEnumerable<T?> GetIncomingRelationshipsAsync<T>(
        string digitalTwinId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $@"MATCH (source:Twin)-[rel]->(target:Twin {{`$dtId`: '{digitalTwinId}'}}) RETURN rel";
        await foreach (JsonElement json in QueryAsync<JsonElement>(cypher, cancellationToken))
        {
            yield return JsonSerializer.Deserialize<T>(json.GetProperty("rel").GetRawText());
        }
    }

    public virtual async Task<T?> CreateOrReplaceRelationshipAsync<T>(
        string digitalTwinId,
        string relationshipId,
        T relationship,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        DateTime now = DateTime.UtcNow;

        string? relationshipJson =
            relationship is string
                ? (string)(object)relationship
                : JsonSerializer.Serialize(relationship);

        // Parse the relationship JSON into a JsonObject
        JsonObject relationshipObject =
            JsonNode.Parse(relationshipJson)?.AsObject()
            ?? throw new ArgumentException("Invalid relationship JSON");

        // Check if $relationshipName is present and matches the arguments
        string relationshipName;
        if (
            relationshipObject.TryGetPropertyValue(
                "$relationshipName",
                out var relationshipNameNode
            ) && relationshipNameNode is JsonValue relationshipNameValue
        )
        {
            relationshipName =
                relationshipNameValue.GetValue<string>()
                ?? throw new ArgumentException(
                    "Relationship's $relationshipName property cannot be null or empty"
                );
        }
        else
        {
            throw new ArgumentException("Relationship's $relationshipName property is missing");
        }
        // Check if $targetId is present and matches the arguments
        string targetId;
        if (
            relationshipObject.TryGetPropertyValue("$targetId", out var targetIdNode)
            && targetIdNode is JsonValue targetIdValue
        )
        {
            targetId =
                targetIdValue.GetValue<string>()
                ?? throw new ArgumentException(
                    "Relationship's $targetId property cannot be null or empty"
                );
        }
        else
        {
            throw new ArgumentException("Relationship's $targetId property is missing");
        }
        // Check if $sourceId is present and matches the arguments
        if (
            relationshipObject.TryGetPropertyValue("$sourceId", out var sourceIdNode)
            && sourceIdNode is JsonValue sourceIdValue
        )
        {
            string sourceId =
                sourceIdValue.GetValue<string>()
                ?? throw new ArgumentException(
                    "Relationship's $sourceId property cannot be null or empty"
                );
            if (sourceId != digitalTwinId)
            {
                throw new ArgumentException(
                    "Provided $sourceId does not match the digitalTwinId argument"
                );
            }
        }
        // Check if $relationshipId is present and matches the arguments
        if (
            relationshipObject.TryGetPropertyValue("$relationshipId", out var relationshipIdNode)
            && relationshipIdNode is JsonValue relationshipIdValue
        )
        {
            string relId =
                relationshipIdValue.GetValue<string>()
                ?? throw new ArgumentException(
                    "Relationship's $relationshipId property cannot be null or empty"
                );
            if (relId != relationshipId)
            {
                throw new ArgumentException(
                    "Provided $relationshipId does not match the relationshipId argument"
                );
            }
        }
        if (!string.IsNullOrEmpty(ifNoneMatch) && !ifNoneMatch.Equals("*"))
        {
            throw new ArgumentException(
                "Invalid If-None-Match header value. Allowed value(s): If-None-Match: *"
            );
        }

        if (ifNoneMatch == "*")
        {
            if (await RelationshipExistsAsync(digitalTwinId, relationshipId, cancellationToken))
            {
                throw new PreconditionFailedException(
                    $"If-None-Match: * header was specified but a relationship with the id {relationshipId} on twin with id {digitalTwinId} was found. Please specify a different twin and relationship id."
                );
            }
        }

        // TODO: Get source and target models and check relationship validity with DTDL parser

        // Ensure $sourceId and $relationshipId are present and correct
        relationshipObject["$sourceId"] = digitalTwinId;
        relationshipObject["$relationshipId"] = relationshipId;
        // Set new etag
        string newEtag = ETagGenerator.GenerateEtag($"{digitalTwinId}-{relationshipId}", now);
        relationshipObject["$etag"] = newEtag;

        string updatedRelationshipJson = JsonSerializer.Serialize(
            relationshipObject,
            serializerOptions
        );

        string cypher =
            $@"WITH '{updatedRelationshipJson}'::agtype as relationship
            MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}}),(target:Twin {{`$dtId`: '{targetId}'}})
            MERGE (source)-[rel:{relationshipName} {{`$relationshipId`: '{relationshipId}'}}]->(target)
            SET rel = relationship
            RETURN rel";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
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
        else
            return default;
    }

    public virtual async Task UpdateRelationshipAsync(
        string digitalTwinId,
        string relationshipId,
        JsonPatch patch,
        string? ifMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        DateTime now = DateTime.UtcNow;

        // Check etag if defined
        if (!string.IsNullOrEmpty(ifMatch) && !ifMatch.Equals("*"))
        {
            if (
                !await RelationshipEtagMatchesAsync(
                    digitalTwinId,
                    relationshipId,
                    ifMatch,
                    cancellationToken
                )
            )
            {
                throw new PreconditionFailedException(
                    $"If-Match: {ifMatch} header value does not match the current ETag value of the relationship twin with id {relationshipId} on twin with id{digitalTwinId}"
                );
            }
        }

        List<string> violations = new();

        List<string> patchOperations = new();

        foreach (var op in patch.Operations)
        {
            var path = op.Path.ToString().TrimStart('/').Replace("/", ".");
            if (path.StartsWith('$'))
            {
                violations.Add($"Cannot update the {path} property");
            }
            if (op.Value != null && (op.Op == OperationType.Add || op.Op == OperationType.Replace))
            {
                if (
                    op.Value.GetValueKind() == JsonValueKind.Object
                    || op.Value.GetValueKind() == JsonValueKind.Array
                )
                {
                    patchOperations.Add(
                        $"SET rel = public.agtype_set(properties(t),['{string.Join("','", path.Split('.'))}'],'{JsonSerializer.Serialize(op.Value, serializerOptions)}')"
                    );
                }
                else if (op.Value.GetValueKind() == JsonValueKind.String)
                {
                    patchOperations.Add(
                        $"SET rel = public.agtype_set(properties(t),['{string.Join("','", path.Split('.'))}'],'{op.Value}')"
                    );
                }
                else
                {
                    patchOperations.Add(
                        $"SET rel = public.agtype_set(properties(t),['{string.Join("','", path.Split('.'))}'],{op.Value})"
                    );
                }
            }
            else if (op.Op == OperationType.Remove)
            {
                patchOperations.Add(
                    $"SET rel = public.agtype_delete_key(properties(t),['{string.Join("','", path.Split('.'))}'])"
                );
            }
            else
            {
                throw new NotSupportedException(
                    $"Operation '{op.Op}' with value '{op.Value}' is not supported"
                );
            }
        }

        string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
        patchOperations.Add($"SET rel.`$etag` = '{newEtag}'");

        string cypher =
            $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel {{`$relationshipId`: '{relationshipId}'}}]->(target:Twin)
            {string.Join("\n", patchOperations)}
            RETURN rel";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new RelationshipNotFoundException(
                $"Relationship with ID {relationshipId} on {digitalTwinId} not found"
            );
        }
    }

    public virtual async Task DeleteRelationshipAsync(
        string digitalTwinId,
        string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $@"MATCH (source:Twin {{`$dtId`: '{digitalTwinId}'}})-[rel {{`$relationshipId`: '{relationshipId}'}}]->(target:Twin) DELETE rel";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            throw new RelationshipNotFoundException(
                $"Relationship with ID {relationshipId} not found"
            );
        }
    }

    #endregion

    #region Models

    public static async IAsyncEnumerable<string> ConvertToAsyncEnumerable(
        IEnumerable<string> source
    )
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }

    public virtual async IAsyncEnumerable<DigitalTwinsModelData> GetModelsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // TODO: Implement dependenciesFor parameter
        string cypher = $@"MATCH (m:Model) RETURN m";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
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
        CancellationToken cancellationToken = default
    )
    {
        string cypher = $@"MATCH (m:Model) WHERE m.id = '{modelId}' RETURN m";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
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

    public virtual async Task<IReadOnlyList<DigitalTwinsModelData>> CreateModelsAsync(
        IEnumerable<string> dtdlModels,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var parsedModels = await _modelParser.ParseAsync(
                ConvertToAsyncEnumerable(dtdlModels),
                cancellationToken: cancellationToken
            );
            IEnumerable<DigitalTwinsModelData> modelDatas = dtdlModels.Select(
                dtdlModel => new DigitalTwinsModelData(dtdlModel)
            );
            // This is needed as after unwinding, it gets converted to agtype again
            string modelsString =
                $"['{string.Join("','", modelDatas.Select(m => JsonSerializer.Serialize(m, serializerOptions).Replace("'", "\\'")))}']";

            // It is not possible to update or overwrite an existing model
            // Trying so will raise a unique constraint violation
            string cypher =
                $@"UNWIND {modelsString} as model
            WITH model::agtype as modelAgtype
            CREATE (m:Model)
            SET m = modelAgtype
            RETURN m";

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCypherCommand(_graphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            List<DigitalTwinsModelData> result = [];
            int k = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                result.Add(new DigitalTwinsModelData(vertex.Properties));
                k++;
            }

            reader.Close();

            List<string> relationshipNames = [];

            foreach (var model in parsedModels)
            {
                // Add edges based on the 'extends' field (especially needed for the 'IS_OF_MODEL' function)
                if (
                    model.Value is DTInterfaceInfo dTInterfaceInfo
                    && dTInterfaceInfo.Extends != null
                    && dTInterfaceInfo.Extends.Count > 0
                )
                {
                    // Get extends and create relationships
                    foreach (var extend in dTInterfaceInfo.Extends)
                    {
                        string extendsCypher =
                            $@"MATCH (m:Model), (m2:Model)
                        WHERE m.id = '{dTInterfaceInfo
                            .Id.AbsoluteUri}' AND m2.id = '{extend.Id.AbsoluteUri}'
                        CREATE (m)-[:_extends]->(m2)";
                        await using var extendsCommand = connection.CreateCypherCommand(
                            _graphName,
                            extendsCypher
                        );
                        // TODO: run these as batch commands
                        await extendsCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // Collect all relationship names so we can prepare the edge labels with replication full
                if (model.Value is DTRelationshipInfo dTRelationshipInfo)
                {
                    if (relationshipNames.Contains(dTRelationshipInfo.Name))
                    {
                        continue;
                    }
                    relationshipNames.Add(dTRelationshipInfo.Name);
                }
            }

            // Run create elabels and then set replication on the new table for each relationship name to ensure replication full
            // Make sure it doesn't fail if the elabel already exists
            foreach (var relationshipName in relationshipNames)
            {
                // Check if label already exists
                await using var labelExistsCommand = new NpgsqlCommand(
                    $@"SELECT EXISTS (SELECT 1 FROM ag_catalog.ag_label WHERE relation = '{_graphName}.""{relationshipName}""'::regclass);",
                    connection
                );
                if ((bool?)await command.ExecuteScalarAsync(cancellationToken) == true)
                {
                    continue;
                }

                // Create new label
                await using var createElabelCommand = new NpgsqlCommand(
                    $@"SELECT create_elabel('{_graphName}', '{relationshipName}');",
                    connection
                );
                await createElabelCommand.ExecuteNonQueryAsync(cancellationToken);

                await using var setReplicationCommand = new NpgsqlCommand(
                    $@"ALTER TABLE {_graphName}.""{relationshipName}"" REPLICA IDENTITY FULL",
                    connection
                );
                await setReplicationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
        catch (PostgresException ex) when (ex.ConstraintName == "model_id_idx")
        {
            throw new ModelAlreadyExistsException(ex.Message);
        }
        catch (ParsingException ex)
        {
            throw new DTDLParserParsingException(ex);
        }
    }

    public virtual async Task DeleteModelAsync(
        string modelId,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: should not be able to delete a model where other models extend from.
        string cypher =
            $@"
            MATCH (m:Model)
            WHERE m.id = '{modelId}' 
            OPTIONAL MATCH (m)-[r:_extends]-()
            DELETE r, m";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            throw new ModelNotFoundException($"Model with ID {modelId} not found");
        }
    }

    #endregion

    #region Query

    public virtual async IAsyncEnumerable<T?> QueryAsync<T>(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        string cypher;
        if (
            query.Contains("SELECT", StringComparison.InvariantCultureIgnoreCase)
            && !query.Contains("RETURN", StringComparison.InvariantCultureIgnoreCase)
        )
        {
            cypher = AdtQueryHelpers.ConvertAdtQueryToCypher(query, _graphName);
        }
        else
        {
            cypher = query;
        }
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken)
            ?? throw new InvalidOperationException("Reader is null");

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
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            valueString
                        );
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
                if (row.Count == 1 && row.TryGetValue("_", out object? value))
                {
                    yield return (T)(object)JsonSerializer.Serialize(value);
                }
                else
                {
                    yield return (T)(object)JsonSerializer.Serialize(row);
                }
            }
            else
            {
                string json;
                if (row.Count == 1 && row.TryGetValue("_", out object? value))
                {
                    json = JsonSerializer.Serialize(value);
                }
                else
                {
                    json = JsonSerializer.Serialize(row);
                }
                yield return JsonSerializer.Deserialize<T>(json);
            }
        }
    }

    #endregion
}
