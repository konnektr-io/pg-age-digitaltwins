using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <summary>
    /// Checks if a digital twin exists asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin to check.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the digital twin exists.</returns>
    private async Task<bool> DigitalTwinExistsAsync(
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId.Replace("'", "\\'")}' RETURN t";
        await using var connection = await _dataSource.OpenConnectionAsync(
            Npgsql.TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if a digital twin's ETag matches asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin to check.</param>
    /// <param name="etag">The ETag to compare.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the ETag matches.</returns>
    private async Task<bool> DigitalTwinEtagMatchesAsync(
        string digitalTwinId,
        string etag,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (t:Twin {{ `$dtId`: '{digitalTwinId.Replace("'", "\\'")}', `$etag`: '{etag}'}}) RETURN t";
        await using var connection = await _dataSource.OpenConnectionAsync(
            Npgsql.TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a digital twin asynchronously.
    /// </summary>
    /// <typeparam name="T">The type to which the digital twin will be deserialized.</typeparam>
    /// <param name="digitalTwinId">The ID of the digital twin to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved digital twin.</returns>
    public virtual async Task<T> GetDigitalTwinAsync<T>(
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "GetDigitalTwinAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);

        try
        {
            string cypher =
                $"MATCH (t:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}}) RETURN t";
            await using var connection = await _dataSource.OpenConnectionAsync(
                Npgsql.TargetSessionAttributes.PreferStandby,
                cancellationToken
            );
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
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
        }
    }

    /// <summary>
    /// Creates or replaces a digital twin asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the digital twin to create or replace.</typeparam>
    /// <param name="digitalTwinId">The ID of the digital twin to create or replace.</param>
    /// <param name="digitalTwin">The digital twin object to create or replace.</param>
    /// <param name="ifNoneMatch">The If-None-Match header value to check for conditional creation.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created or replaced digital twin.</returns>
    public virtual async Task<T?> CreateOrReplaceDigitalTwinAsync<T>(
        string digitalTwinId,
        T digitalTwin,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "CreateOrReplaceDigitalTwinAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("ifNoneMatch", ifNoneMatch);

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
            DigitalTwinsModelData modelData =
                await GetModelWithCacheAsync(modelId, cancellationToken)
                ?? throw new ModelNotFoundException($"{modelId} does not exist.");
            IReadOnlyDictionary<Dtmi, DTEntityInfo> parsedModelEntities =
                await _modelParser.ParseAsync(
                    modelData.DtdlModel,
                    cancellationToken: cancellationToken
                );
            DTInterfaceInfo dtInterfaceInfo =
                (DTInterfaceInfo)
                    parsedModelEntities.FirstOrDefault(e => e.Value is DTInterfaceInfo).Value
                ?? throw new ModelNotFoundException(
                    $"{modelId} or one of its dependencies does not exist."
                );
            List<string> violations = new();

            foreach (KeyValuePair<string, JsonNode?> kv in digitalTwinObject)
            {
                string property = kv.Key;

                if (
                    property == "$metadata"
                    || property == "$dtId"
                    || property == "$etag"
                    || property == "$lastUpdateTime"
                )
                {
                    continue;
                }

                if (!dtInterfaceInfo.Contents.TryGetValue(property, out DTContentInfo? contentInfo))
                {
                    violations.Add($"Property '{property}' is not defined in the model");
                    continue;
                }

                JsonElement value = kv.Value.ToJsonDocument().RootElement;

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
MERGE (t: Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})
SET t = twin
RETURN t";
            await using var connection = await _dataSource.OpenConnectionAsync(
                Npgsql.TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
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
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
        }
    }

    /// <summary>
    /// Updates a digital twin asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin to update.</param>
    /// <param name="patch">The JSON patch document containing the updates.</param>
    /// <param name="ifMatch">The If-Match header value to check for conditional updates.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task UpdateDigitalTwinAsync(
        string digitalTwinId,
        JsonPatch patch,
        string? ifMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "UpdateDigitalTwinAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);

        try
        {
            DateTime now = DateTime.UtcNow;

            // Retrieve the current twin
            var currentTwin =
                await GetDigitalTwinAsync<JsonObject>(digitalTwinId, cancellationToken)
                ?? throw new DigitalTwinNotFoundException(
                    $"Digital Twin with ID {digitalTwinId} not found"
                );

            // Check if etag matches if If-Match header is provided
            if (!string.IsNullOrEmpty(ifMatch) && !ifMatch.Equals("*"))
            {
                if (
                    currentTwin.TryGetPropertyValue("$etag", out JsonNode? etagNode)
                    && etagNode is JsonValue etagValue
                    && etagValue.GetValueKind() == JsonValueKind.String
                    && !etagValue.ToString().Equals(ifMatch, StringComparison.Ordinal)
                )
                {
                    throw new PreconditionFailedException(
                        $"If-Match: {ifMatch} header value does not match the current ETag value of the digital twin with id {digitalTwinId}"
                    );
                }
            }

            JsonNode patchedTwinNode = currentTwin.DeepClone();
            try
            {
                var patchResult = patch.Apply(patchedTwinNode);
                if (patchResult.Result is null)
                {
                    throw new ValidationFailedException("Failed to apply patch: result is null");
                }
                patchedTwinNode = patchResult.Result;
            }
            catch (Exception ex)
            {
                throw new ValidationFailedException($"Failed to apply patch: {ex.Message}");
            }
            if (patchedTwinNode is not JsonObject patchedTwin)
            {
                throw new ValidationFailedException("Patched twin is not a valid object");
            }

            if (
                !patchedTwin.TryGetPropertyValue("$dtId", out JsonNode? dtIdNode)
                || dtIdNode is not JsonValue
            )
            {
                patchedTwin["$dtId"] = digitalTwinId;
            }

            if (
                !patchedTwin.TryGetPropertyValue("$metadata", out JsonNode? metaNode)
                || metaNode is not JsonObject metadataObject
            )
            {
                throw new ValidationFailedException(
                    "Digital Twin must have a $metadata property of type object"
                );
            }
            if (
                !metadataObject.TryGetPropertyValue("$model", out JsonNode? modelNode2)
                || modelNode2 is not JsonValue modelValue
                || modelValue.GetValueKind() != JsonValueKind.String
            )
            {
                throw new ValidationFailedException(
                    "Digital Twin's $metadata must contain a $model property of type string"
                );
            }
            string modelId =
                modelValue.ToString()
                ?? throw new ValidationFailedException(
                    "Digital Twin's $model property cannot be null or empty"
                );
            DigitalTwinsModelData modelData =
                await GetModelWithCacheAsync(modelId, cancellationToken)
                ?? throw new ModelNotFoundException($"{modelId} does not exist.");
            IReadOnlyDictionary<Dtmi, DTEntityInfo> parsedModelEntities =
                await _modelParser.ParseAsync(
                    modelData.DtdlModel,
                    cancellationToken: cancellationToken
                );
            DTInterfaceInfo dtInterfaceInfo =
                (DTInterfaceInfo)
                    parsedModelEntities.FirstOrDefault(e => e.Value is DTInterfaceInfo).Value
                ?? throw new ModelNotFoundException(
                    $"{modelId} or one of its dependencies does not exist."
                );
            List<string> violations = new();
            // Track which properties were changed by the patch
            var changedProperties = new HashSet<string>(
                patch
                    .Operations.Where(op =>
                        op.Op == OperationType.Add
                        || op.Op == OperationType.Replace
                        || op.Op == OperationType.Remove
                    )
                    .Select(op => op.Path.ToString().TrimStart('/').Split('/')[0])
            );
            foreach (KeyValuePair<string, JsonNode?> kv in patchedTwin)
            {
                string property = kv.Key;
                if (
                    property == "$metadata"
                    || property == "$dtId"
                    || property == "$etag"
                    || property == "$lastUpdateTime"
                )
                {
                    continue;
                }
                if (!dtInterfaceInfo.Contents.TryGetValue(property, out DTContentInfo? contentInfo))
                {
                    violations.Add($"Property '{property}' is not defined in the model");
                    continue;
                }
                JsonElement value = kv.Value.ToJsonDocument().RootElement;
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
                        // Only update lastUpdateTime if property was changed by the patch
                        if (changedProperties.Contains(property))
                        {
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
                }
                /* else if (contentInfo is DTComponentInfo componentDef)
                {
                    // kv.Value should be a JsonObject representing the component

                    // Get DTInterfaceInfo for the component

                    // Iterate over the properties of the component and validate them

                } */
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
            patchedTwin["$etag"] = newEtag;
            // Replace the entire twin in the database
            string updatedDigitalTwinJson = JsonSerializer
                .Serialize(patchedTwin, serializerOptions)
                .Replace("'", "\\'");
            string cypher =
                $@"WITH '{updatedDigitalTwinJson}'::agtype as twin
MERGE (t: Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})
SET t = twin";
            await using var connection = await _dataSource.OpenConnectionAsync(
                Npgsql.TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = connection.CreateCypherCommand(_graphName, cypher);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (ModelNotFoundException ex)
        {
            throw new ValidationFailedException(ex.Message);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
        }
    }

    /// <summary>
    /// Deletes a digital twin asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin to delete.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task DeleteDigitalTwinAsync(
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "DeleteDigitalTwinAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);

        try
        {
            string cypher =
                $@"MATCH (t:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}}) 
DELETE t
RETURN COUNT(t) AS deletedCount";
            await using var connection = await _dataSource.OpenConnectionAsync(
                Npgsql.TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = connection.CreateCypherCommand(_graphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            int rowsAffected = 0;
            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
                rowsAffected = (int)agResult;
            }
            if (rowsAffected <= 0)
            {
                throw new DigitalTwinNotFoundException(
                    $"Digital Twin with ID {digitalTwinId} not found"
                );
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
        }
    }
}
