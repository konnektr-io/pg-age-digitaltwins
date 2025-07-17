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
using Npgsql;
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
        NpgsqlConnection connection,
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (t:Twin) WHERE t['$dtId'] = '{digitalTwinId.Replace("'", "\\'")}' RETURN t";
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
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.PreferStandby,
                cancellationToken
            );
            return await GetDigitalTwinAsync<T>(connection, digitalTwinId, cancellationToken)
                    .ConfigureAwait(false)
                ?? throw new DigitalTwinNotFoundException(
                    $"Digital Twin with ID {digitalTwinId} not found"
                );
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

    internal async Task<T?> GetDigitalTwinAsync<T>(
        NpgsqlConnection connection,
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (t:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}}) RETURN t";
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
            var vertex = (Vertex)agResult;
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex.Properties))
                ?? throw new SerializationException(
                    $"Digital Twin with ID {digitalTwinId} could not be deserialized"
                );
        }
        else
        {
            return default;
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
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.PreferStandby,
                cancellationToken
            );
            return await CreateOrReplaceDigitalTwinAsync(
                    connection,
                    digitalTwinId,
                    digitalTwin,
                    ifNoneMatch,
                    cancellationToken
                )
                .ConfigureAwait(false);
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

    internal async Task<T?> CreateOrReplaceDigitalTwinAsync<T>(
        NpgsqlConnection connection,
        string digitalTwinId,
        T digitalTwin,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default
    )
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
            throw new ArgumentException("Provided digitalTwinId does not match the $dtId property");
        }
        if (!string.IsNullOrEmpty(ifNoneMatch) && !ifNoneMatch.Equals("*"))
        {
            throw new ArgumentException(
                "Invalid If-None-Match header value. Allowed value(s): If-None-Match: *"
            );
        }

        if (ifNoneMatch == "*")
        {
            if (await DigitalTwinExistsAsync(connection, digitalTwinId, cancellationToken))
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
        IReadOnlyDictionary<Dtmi, DTEntityInfo> parsedModelEntities = await _modelParser.ParseAsync(
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
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex.Properties));
            }
        }
        else
            return default;
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
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await UpdateDigitalTwinAsync(
                    connection,
                    digitalTwinId,
                    patch,
                    ifMatch,
                    cancellationToken
                )
                .ConfigureAwait(false);
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

    internal async Task UpdateDigitalTwinAsync(
        NpgsqlConnection connection,
        string digitalTwinId,
        JsonPatch patch,
        string? ifMatch = null,
        CancellationToken cancellationToken = default
    )
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
        IReadOnlyDictionary<Dtmi, DTEntityInfo> parsedModelEntities = await _modelParser.ParseAsync(
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
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await DeleteDigitalTwinAsync(connection, digitalTwinId, cancellationToken)
                .ConfigureAwait(false);
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

    internal async Task DeleteDigitalTwinAsync(
        NpgsqlConnection connection,
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $@"MATCH (t:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}}) 
DELETE t
RETURN COUNT(t) AS deletedCount";
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

    /// <summary>
    /// Creates or replaces multiple digital twins asynchronously in a batch operation.
    /// </summary>
    /// <typeparam name="T">The type of the digital twins to create or replace.</typeparam>
    /// <param name="digitalTwins">The digital twins to create or replace.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the batch operation results.</returns>
    /// <exception cref="ArgumentException">Thrown when the batch size exceeds the maximum allowed size (100).</exception>
    public virtual async Task<BatchDigitalTwinResult> CreateOrReplaceDigitalTwinsAsync<T>(
        IEnumerable<T> digitalTwins,
        CancellationToken cancellationToken = default
    )
    {
        const int MaxBatchSize = 100;
        var digitalTwinsList = digitalTwins.ToList();

        using var activity = ActivitySource.StartActivity(
            "CreateOrReplaceDigitalTwinsAsync",
            ActivityKind.Client
        );
        activity?.SetTag("batchSize", digitalTwinsList.Count);

        try
        {
            // Validate batch size
            if (digitalTwinsList.Count > MaxBatchSize)
            {
                throw new ArgumentException(
                    $"Batch size ({digitalTwinsList.Count}) exceeds maximum allowed size ({MaxBatchSize})"
                );
            }

            if (digitalTwinsList.Count == 0)
            {
                return new BatchDigitalTwinResult(Array.Empty<DigitalTwinOperationResult>());
            }

            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );

            return await CreateOrReplaceDigitalTwinsInternalAsync(
                connection,
                digitalTwinsList,
                cancellationToken
            );
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

    private async Task<BatchDigitalTwinResult> CreateOrReplaceDigitalTwinsInternalAsync<T>(
        NpgsqlConnection connection,
        IList<T> digitalTwins,
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<DigitalTwinOperationResult>();
        var validTwins = new List<(string digitalTwinId, JsonObject digitalTwinObject)>();
        DateTime now = DateTime.UtcNow;

        // Phase 1: Pre-validation - Basic JSON structure and metadata validation
        foreach (var digitalTwin in digitalTwins)
        {
            string digitalTwinId = string.Empty;

            try
            {
                // Convert to JSON string
                string digitalTwinJson =
                    digitalTwin is string
                        ? (string)(object)digitalTwin
                        : JsonSerializer.Serialize(digitalTwin);

                // Parse and validate JSON structure
                JsonObject digitalTwinObject =
                    JsonNode.Parse(digitalTwinJson)?.AsObject()
                    ?? throw new ArgumentException("Invalid digital twin JSON");

                // Extract $dtId from the twin object
                if (
                    digitalTwinObject.TryGetPropertyValue("$dtId", out JsonNode? dtIdNode)
                    && dtIdNode is JsonValue dtIdValue
                )
                {
                    digitalTwinId = dtIdValue.ToString();
                }
                else
                {
                    throw new ArgumentException("Digital twin must have a $dtId property");
                }

                // Validate $metadata property
                if (
                    !digitalTwinObject.TryGetPropertyValue("$metadata", out JsonNode? metadataNode)
                    || metadataNode is not JsonObject metadataObject
                )
                {
                    throw new ArgumentException(
                        "Digital Twin must have a $metadata property of type object"
                    );
                }

                // Validate $model property
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

                validTwins.Add((digitalTwinId, digitalTwinObject));
            }
            catch (Exception ex)
            {
                results.Add(DigitalTwinOperationResult.Failure(digitalTwinId, ex.Message));
            }
        }

        if (validTwins.Count == 0)
        {
            return new BatchDigitalTwinResult(results);
        }

        // Phase 2: Load and cache all unique models
        var modelCache = new Dictionary<string, DTInterfaceInfo>();
        var uniqueModelIds = validTwins
            .Select(t => t.digitalTwinObject["$metadata"]!["$model"]!.ToString())
            .Distinct()
            .ToList();

        foreach (string modelId in uniqueModelIds)
        {
            try
            {
                var modelData = await GetModelWithCacheAsync(modelId, cancellationToken);
                if (modelData == null)
                {
                    throw new ModelNotFoundException($"{modelId} does not exist.");
                }

                var parsedModelEntities = await _modelParser.ParseAsync(
                    modelData.DtdlModel,
                    cancellationToken: cancellationToken
                );

                var dtInterfaceInfo = (DTInterfaceInfo)
                    parsedModelEntities.FirstOrDefault(e => e.Value is DTInterfaceInfo).Value;

                if (dtInterfaceInfo == null)
                {
                    throw new ModelNotFoundException(
                        $"{modelId} or one of its dependencies does not exist."
                    );
                }

                modelCache[modelId] = dtInterfaceInfo;
            }
            catch (Exception ex)
            {
                // Mark all twins using this model as failed
                var failedTwins = validTwins
                    .Where(t => t.digitalTwinObject["$metadata"]!["$model"]!.ToString() == modelId)
                    .Select(t => t.digitalTwinId)
                    .ToList();

                foreach (string twinId in failedTwins)
                {
                    results.Add(DigitalTwinOperationResult.Failure(twinId, ex.Message));
                }

                // Remove failed twins from processing
                validTwins.RemoveAll(t =>
                    t.digitalTwinObject["$metadata"]!["$model"]!.ToString() == modelId
                );
            }
        }

        // Phase 3: Validate each twin against its model schema
        var finalValidTwins = new List<(string digitalTwinId, JsonObject digitalTwinObject)>();

        foreach (var (digitalTwinId, digitalTwinObject) in validTwins)
        {
            try
            {
                string modelId = digitalTwinObject["$metadata"]!["$model"]!.ToString();
                var dtInterfaceInfo = modelCache[modelId];
                var metadataObject = digitalTwinObject["$metadata"]!.AsObject();
                var violations = new List<string>();

                // Validate all properties against the model
                foreach (var kvp in digitalTwinObject)
                {
                    string property = kvp.Key;

                    if (
                        property == "$metadata"
                        || property == "$dtId"
                        || property == "$etag"
                        || property == "$lastUpdateTime"
                    )
                    {
                        continue;
                    }

                    if (
                        !dtInterfaceInfo.Contents.TryGetValue(
                            property,
                            out DTContentInfo? contentInfo
                        )
                    )
                    {
                        violations.Add($"Property '{property}' is not defined in the model");
                        continue;
                    }

                    JsonElement value = kvp.Value.ToJsonDocument().RootElement;

                    if (contentInfo is DTPropertyInfo propertyDef)
                    {
                        var validationFailures = propertyDef.Schema.ValidateInstance(value);
                        if (validationFailures.Count != 0)
                        {
                            violations.AddRange(
                                validationFailures.Select(v => $"Property '{property}': {v}")
                            );
                        }
                        else
                        {
                            // Set last update time for valid property
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

                // Set global metadata
                metadataObject["$lastUpdateTime"] = now.ToString("o");
                string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
                digitalTwinObject["$etag"] = newEtag;

                finalValidTwins.Add((digitalTwinId, digitalTwinObject));
            }
            catch (Exception ex)
            {
                results.Add(DigitalTwinOperationResult.Failure(digitalTwinId, ex.Message));
            }
        }

        // Phase 4: Execute batch database operation
        if (finalValidTwins.Count > 0)
        {
            try
            {
                // Prepare twins for batch insert - construct full query like models
                string twinsString =
                    $"['{string.Join("','", finalValidTwins.Select(t => JsonSerializer.Serialize(t.digitalTwinObject, serializerOptions).Replace("'", "\\'")))}']";

                string cypher =
                    $@"UNWIND {twinsString} as twinJson
                    WITH twinJson::agtype as twin
                    MERGE (t:Twin {{`$dtId`: twin['$dtId']}})
                    SET t = twin";

                await using var command = connection.CreateCypherCommand(_graphName, cypher);
                await command.ExecuteNonQueryAsync(cancellationToken);

                // Mark all successfully processed twins
                foreach (var (digitalTwinId, _) in finalValidTwins)
                {
                    results.Add(DigitalTwinOperationResult.Success(digitalTwinId));
                }
            }
            catch (Exception ex)
            {
                // Mark all remaining twins as failed due to database error
                foreach (var (digitalTwinId, _) in finalValidTwins)
                {
                    results.Add(
                        DigitalTwinOperationResult.Failure(
                            digitalTwinId,
                            $"Database error: {ex.Message}"
                        )
                    );
                }
            }
        }

        return new BatchDigitalTwinResult(results);
    }
}
