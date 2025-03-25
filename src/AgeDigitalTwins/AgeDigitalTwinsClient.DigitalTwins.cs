using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using DTDLParser;
using DTDLParser.Models;
using Json.More;
using Json.Patch;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
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
                throw new Exceptions.NotSupportedException(
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
}
