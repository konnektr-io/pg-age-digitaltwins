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
using Microsoft.Extensions.Caching.Memory;
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
        if (_twinCacheExpiration != TimeSpan.Zero && _twinCache.TryGetValue(digitalTwinId, out object? cachedExists))
        {
            return (bool)cachedExists!;
        }

        string cypher = "MATCH (t:Twin {`$dtId`: $twinId}) RETURN t";
        await using var command = connection.CreateCypherCommand(
            _graphName, cypher,
            new Dictionary<string, object?> { { DigitalTwinsJsonPropertyNames.TwinIdParameter, digitalTwinId } }
        );
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        bool exists = await reader.ReadAsync(cancellationToken);

        if (_twinCacheExpiration != TimeSpan.Zero)
        {
            _twinCache.Set(digitalTwinId, exists, _twinCacheExpiration);
        }

        return exists;
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
                    DiagnosticConstants.ActivityEventException,
                    default,
                    new ActivityTagsCollection
                    {
                        { DiagnosticConstants.ActivityTagExceptionType, ex.GetType().FullName },
                        { DiagnosticConstants.ActivityTagExceptionMessage, ex.Message },
                        { DiagnosticConstants.ActivityTagExceptionStackTrace, ex.StackTrace },
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
        string cypher = "MATCH (t:Twin {`$dtId`: $twinId}) RETURN t";
        await using var command = connection.CreateCypherCommand(
            _graphName, cypher,
            new Dictionary<string, object?> { { DigitalTwinsJsonPropertyNames.TwinIdParameter, digitalTwinId } }
        );
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
            var vertex = (Vertex)agResult!;
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex!.Properties))
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
    /// <param name="userId">The ID of the user performing the operation, stored in property metadata when <see cref="AgeDigitalTwinsClientOptions.TrackLastUpdatedBy"/> is enabled.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created or replaced digital twin.</returns>
    public virtual async Task<T?> CreateOrReplaceDigitalTwinAsync<T>(
        string digitalTwinId,
        T digitalTwin,
        string? ifNoneMatch = null,
        string? userId = null,
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
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            return await CreateOrReplaceDigitalTwinAsync(
                    connection,
                    digitalTwinId,
                    digitalTwin,
                    ifNoneMatch,
                    userId,
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
                    DiagnosticConstants.ActivityEventException,
                    default,
                    new ActivityTagsCollection
                    {
                        { DiagnosticConstants.ActivityTagExceptionType, ex.GetType().FullName },
                        { DiagnosticConstants.ActivityTagExceptionMessage, ex.Message },
                        { DiagnosticConstants.ActivityTagExceptionStackTrace, ex.StackTrace },
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
        string? userId = null,
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
            !digitalTwinObject.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata, out JsonNode? metadataNode)
            || metadataNode is not JsonObject metadataObject
        )
        {
            throw new ArgumentException(
                "Digital Twin must have a $metadata property of type object"
            );
        }
        if (
            !metadataObject.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.MetadataModel, out JsonNode? modelNode)
            || modelNode is not JsonValue modelValue
            || modelValue.GetValueKind() != JsonValueKind.String
        )
        {
            throw new ArgumentException(
                "Digital Twin's $metadata must contain a $model property of type string"
            );
        }
        if (
            digitalTwinObject.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinId, out JsonNode? dtIdNode)
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

        if (ifNoneMatch == "*" &&  await DigitalTwinExistsAsync(connection, digitalTwinId, cancellationToken))
        {
            throw new PreconditionFailedException(
                $"If-None-Match: * header was specified but a twin with the id {digitalTwinId} was found. Please specify a different twin id."
            );
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
                property == DigitalTwinsJsonPropertyNames.DigitalTwinMetadata
                || property == DigitalTwinsJsonPropertyNames.DigitalTwinId
                || property == DigitalTwinsJsonPropertyNames.DigitalTwinETag
                || property == DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime
                || property == DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy
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
                        metadataPropertyObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o");
                        if (_trackLastUpdatedBy && userId != null)
                        {
                            metadataPropertyObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                        }
                    }
                    else
                    {
                        var newPropertyMetadata = new JsonObject
                        {
                            [DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o"),
                        };
                        if (_trackLastUpdatedBy && userId != null)
                        {
                            newPropertyMetadata[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                        }
                        metadataObject[property] = newPropertyMetadata;
                    }
                }
            }
            else if (contentInfo is DTComponentInfo componentDef)
            {
                // kv.Value should be a JsonObject representing the component
                if (kv.Value is not JsonObject componentObject)
                {
                    violations.Add($"Component '{property}' must be a JSON object");
                    continue;
                }

                // Get the component schema (DTInterfaceInfo)
                if (componentDef.Schema is not DTInterfaceInfo componentSchema)
                {
                    violations.Add(
                        $"Component '{property}' does not have a valid interface schema"
                    );
                    continue;
                }

                // Validate each property in the component against the component schema
                foreach (var componentKv in componentObject)
                {
                    string componentProperty = componentKv.Key;

                    // Skip metadata properties
                    if (componentProperty == DigitalTwinsJsonPropertyNames.DigitalTwinMetadata)
                    {
                        continue;
                    }

                    // Check if the property is defined in the component schema
                    if (
                        !componentSchema.Contents.TryGetValue(
                            componentProperty,
                            out DTContentInfo? componentContentInfo
                        )
                    )
                    {
                        violations.Add(
                            $"Component '{property}' property '{componentProperty}' is not defined in the component schema"
                        );
                        continue;
                    }

                    // Validate properties within the component
                    if (
                        componentContentInfo is DTPropertyInfo componentPropertyDef
                        && componentKv.Value != null
                    )
                    {
                        JsonElement componentValue = componentKv.Value.ToJsonDocument().RootElement;
                        var componentValidationFailures =
                            componentPropertyDef.Schema.ValidateInstance(componentValue);

                        if (componentValidationFailures.Count != 0)
                        {
                            violations.AddRange(
                                componentValidationFailures.Select(v =>
                                    $"Component '{property}' property '{componentProperty}': {v}"
                                )
                            );
                        }
                    }
                }

                // Set component metadata
                if (
                    !componentObject.TryGetPropertyValue(
                        DigitalTwinsJsonPropertyNames.DigitalTwinMetadata,
                        out JsonNode? componentMetadataNode
                    ) || componentMetadataNode is not JsonObject componentMetadataObject
                )
                {
                    var newComponentMetadata = new JsonObject
                    {
                        [DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o"),
                    };
                    if (_trackLastUpdatedBy && userId != null)
                    {
                        newComponentMetadata[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                    }
                    componentObject[DigitalTwinsJsonPropertyNames.DigitalTwinMetadata] = newComponentMetadata;
                }
                else
                {
                    componentMetadataObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o");
                    if (_trackLastUpdatedBy && userId != null)
                    {
                        componentMetadataObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                    }
                }

                // Set component metadata in the twin's metadata
                if (
                    metadataObject.TryGetPropertyValue(property, out JsonNode? metadataPropertyNode)
                    && metadataPropertyNode is JsonObject metadataPropertyObject
                )
                {
                    metadataPropertyObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o");
                    if (_trackLastUpdatedBy && userId != null)
                    {
                        metadataPropertyObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                    }
                }
                else
                {
                    var newPropertyMetadata = new JsonObject
                    {
                        [DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o"),
                    };
                    if (_trackLastUpdatedBy && userId != null)
                    {
                        newPropertyMetadata[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                    }
                    metadataObject[property] = newPropertyMetadata;
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
        metadataObject[DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime] = now.ToString("o");
        // Set new etag
        string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
        digitalTwinObject[DigitalTwinsJsonPropertyNames.DigitalTwinETag] = newEtag;
        if (_trackLastUpdatedBy && userId != null)
        {
            digitalTwinObject.Remove(DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy);
            metadataObject[DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy] = userId;
        }

        string updatedTwinJson = JsonSerializer.Serialize(digitalTwinObject);

        // Invalidate cache
        _twinCache.Remove(digitalTwinId);

        string cypher =
            @"WITH $twinJson::cstring::agtype as twin
MERGE (t: Twin {`$dtId`: $twinId})
SET t = twin
RETURN t";
        await using var command = connection.CreateCypherCommand(
            _graphName, cypher,
            new Dictionary<string, object?>
            {
                { DigitalTwinsJsonPropertyNames.TwinIdParameter, digitalTwinId },
                { "twinJson", updatedTwinJson },
            }
        );
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
            var vertex = (Vertex)agResult!;

            if (typeof(T) == typeof(string))
            {
                return (T)(object)JsonSerializer.Serialize(vertex!.Properties);
            }
            else
            {
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(vertex!.Properties));
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
    /// <param name="userId">The ID of the user performing the operation, stored in property metadata when <see cref="AgeDigitalTwinsClientOptions.TrackLastUpdatedBy"/> is enabled.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task UpdateDigitalTwinAsync(
        string digitalTwinId,
        JsonPatch patch,
        string? ifMatch = null,
        string? userId = null,
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
                    userId,
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
                    DiagnosticConstants.ActivityEventException,
                    default,
                    new ActivityTagsCollection
                    {
                        { DiagnosticConstants.ActivityTagExceptionType, ex.GetType().FullName },
                        { DiagnosticConstants.ActivityTagExceptionMessage, ex.Message },
                        { DiagnosticConstants.ActivityTagExceptionStackTrace, ex.StackTrace },
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
        string? userId = null,
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
        if (!string.IsNullOrEmpty(ifMatch) 
                && !ifMatch.Equals("*") 
                && currentTwin.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinETag, out JsonNode? etagNode)
                && etagNode is JsonValue etagValue
                && etagValue.GetValueKind() == JsonValueKind.String
                && !etagValue.ToString().Equals(ifMatch, StringComparison.Ordinal))
        {
            throw new PreconditionFailedException(
                $"If-Match: {ifMatch} header value does not match the current ETag value of the digital twin with id {digitalTwinId}"
            );
        }

        JsonNode patchedTwinNode = currentTwin.DeepClone();
        try
        {
            var patchResult = patch.Apply(patchedTwinNode);
            if (!string.IsNullOrEmpty(patchResult.Error))
            {
                throw new ValidationFailedException($"Failed to apply patch: {patchResult.Error}");
            }
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
            !patchedTwin.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinId, out JsonNode? dtIdNode)
            || dtIdNode is not JsonValue
        )
        {
            patchedTwin[DigitalTwinsJsonPropertyNames.DigitalTwinId] = digitalTwinId;
        }

        if (
            !patchedTwin.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata, out JsonNode? metaNode)
            || metaNode is not JsonObject metadataObject
        )
        {
            throw new ValidationFailedException(
                "Digital Twin must have a $metadata property of type object"
            );
        }
        if (
            !metadataObject.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.MetadataModel, out JsonNode? modelNode2)
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
                property == DigitalTwinsJsonPropertyNames.DigitalTwinMetadata
                || property == DigitalTwinsJsonPropertyNames.DigitalTwinId
                || property == DigitalTwinsJsonPropertyNames.DigitalTwinETag
                || property == DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime
                || property == DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy
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
                            metadataPropertyObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o");
                            if (_trackLastUpdatedBy && userId != null)
                            {
                                metadataPropertyObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                            }
                        }
                        else
                        {
                            var newPropertyMetadata = new JsonObject
                            {
                                [DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o"),
                            };
                            if (_trackLastUpdatedBy && userId != null)
                            {
                                newPropertyMetadata[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdatedBy] = userId;
                            }
                            metadataObject[property] = newPropertyMetadata;
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
        metadataObject[DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime] = now.ToString("o");
        // Set new etag
        string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
        patchedTwin[DigitalTwinsJsonPropertyNames.DigitalTwinETag] = newEtag;
        if (_trackLastUpdatedBy && userId != null)
        {
            patchedTwin.Remove(DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy);
            metadataObject[DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy] = userId;
        }
        string updatedTwinJson = JsonSerializer.Serialize(patchedTwin);

        string cypher =
            @"WITH $twinJson::cstring::agtype as twin
MERGE (t: Twin {`$dtId`: $twinId})
SET t = twin";
        await using var command = connection.CreateCypherCommand(
            _graphName, cypher,
            new Dictionary<string, object?>
            {
                { DigitalTwinsJsonPropertyNames.TwinIdParameter, digitalTwinId },
                { "twinJson", updatedTwinJson },
            }
        );
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
                    DiagnosticConstants.ActivityEventException,
                    default,
                    new ActivityTagsCollection
                    {
                        { DiagnosticConstants.ActivityTagExceptionType, ex.GetType().FullName },
                        { DiagnosticConstants.ActivityTagExceptionMessage, ex.Message },
                        { DiagnosticConstants.ActivityTagExceptionStackTrace, ex.StackTrace },
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
        // Invalidate cache
        _twinCache.Remove(digitalTwinId);

        string cypher =
            @"MATCH (t:Twin {`$dtId`: $twinId}) 
DELETE t
RETURN COUNT(t) AS deletedCount";
        await using var command = connection.CreateCypherCommand(
            _graphName, cypher,
            new Dictionary<string, object?> { { DigitalTwinsJsonPropertyNames.TwinIdParameter, digitalTwinId } }
        );
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        int rowsAffected = 0;
        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
            rowsAffected = (int)agResult!;
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
        string? userId = null,
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
                userId,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    DiagnosticConstants.ActivityEventException,
                    default,
                    new ActivityTagsCollection
                    {
                        { DiagnosticConstants.ActivityTagExceptionType, ex.GetType().FullName },
                        { DiagnosticConstants.ActivityTagExceptionMessage, ex.Message },
                        { DiagnosticConstants.ActivityTagExceptionStackTrace, ex.StackTrace },
                    }
                )
            );
            throw;
        }
    }

    internal async Task<BatchDigitalTwinResult> CreateOrReplaceDigitalTwinsInternalAsync<T>(
        NpgsqlConnection connection,
        IList<T> digitalTwins,
        string? userId = null,
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
                    digitalTwinObject.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinId, out JsonNode? dtIdNode)
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
                    !digitalTwinObject.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata, out JsonNode? metadataNode)
                    || metadataNode is not JsonObject metadataObject
                )
                {
                    throw new ArgumentException(
                        "Digital Twin must have a $metadata property of type object"
                    );
                }

                // Validate $model property
                if (
                    !metadataObject.TryGetPropertyValue(DigitalTwinsJsonPropertyNames.MetadataModel, out JsonNode? modelNode)
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
            .Select(t => t.digitalTwinObject[DigitalTwinsJsonPropertyNames.DigitalTwinMetadata]![DigitalTwinsJsonPropertyNames.MetadataModel]!.ToString())
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
                    .Where(t => t.digitalTwinObject[DigitalTwinsJsonPropertyNames.DigitalTwinMetadata]![DigitalTwinsJsonPropertyNames.MetadataModel]!.ToString() == modelId)
                    .Select(t => t.digitalTwinId)
                    .ToList();

                foreach (string twinId in failedTwins)
                {
                    results.Add(DigitalTwinOperationResult.Failure(twinId, ex.Message));
                }

                // Remove failed twins from processing
                validTwins.RemoveAll(t =>
                    t.digitalTwinObject[DigitalTwinsJsonPropertyNames.DigitalTwinMetadata]![DigitalTwinsJsonPropertyNames.MetadataModel]!.ToString() == modelId
                );
            }
        }

        // Phase 3: Validate each twin against its model schema
        var finalValidTwins = new List<(string digitalTwinId, JsonObject digitalTwinObject)>();

        foreach (var (digitalTwinId, digitalTwinObject) in validTwins)
        {
            try
            {
                string modelId = digitalTwinObject[DigitalTwinsJsonPropertyNames.DigitalTwinMetadata]![DigitalTwinsJsonPropertyNames.MetadataModel]!.ToString();
                var dtInterfaceInfo = modelCache[modelId];
                var metadataObject = digitalTwinObject[DigitalTwinsJsonPropertyNames.DigitalTwinMetadata]!.AsObject();
                var violations = new List<string>();

                // Validate all properties against the model
                foreach (var kvp in digitalTwinObject)
                {
                    string property = kvp.Key;

                    if (
                        property == DigitalTwinsJsonPropertyNames.DigitalTwinMetadata
                        || property == DigitalTwinsJsonPropertyNames.DigitalTwinId
                        || property == DigitalTwinsJsonPropertyNames.DigitalTwinETag
                        || property == DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime
                        || property == DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy
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
                                metadataPropertyObject[DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o");
                            }
                            else
                            {
                                metadataObject[property] = new JsonObject
                                {
                                    [DigitalTwinsJsonPropertyNames.MetadataPropertyLastUpdateTime] = now.ToString("o"),
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
                metadataObject[DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime] = now.ToString("o");
                string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
                digitalTwinObject[DigitalTwinsJsonPropertyNames.DigitalTwinETag] = newEtag;
                if (_trackLastUpdatedBy && userId != null)
                {
                    digitalTwinObject.Remove(DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy);
                    metadataObject[DigitalTwinsJsonPropertyNames.MetadataLastUpdatedBy] = userId;
                }

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
                var twinJsonStrings = finalValidTwins
                    .Select(t => JsonSerializer.Serialize(t.digitalTwinObject))
                    .ToList();

                string cypher =
                    @"UNWIND $twinJsonStrings as twinJson
WITH twinJson::cstring::agtype as twin
MERGE (t:Twin {`$dtId`: twin['$dtId']})
SET t = twin";

                // Invalidate cache for all twins in the batch
                foreach (var (twinId, _) in finalValidTwins)
                {
                    _twinCache.Remove(twinId);
                }

                await using var command = connection.CreateCypherCommand(
                    _graphName, cypher,
                    new Dictionary<string, object?>
                    {
                        { "twinJsonStrings", twinJsonStrings },
                    }
                );
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

    /// <summary>
    /// Explores the graph neighborhood of a specific twin.
    /// </summary>
    public virtual async Task<string> ExploreGraphNeighborhoodAsync(
        string twinId,
        int hops,
        CancellationToken cancellationToken = default
    )
    {
        if (hops < 1)
            hops = 1;

        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );

        string cypher =
            $@"MATCH path = (t:Twin {{`$dtId`: $twinId}})-[r*1..{hops}]-(n:Twin) 
            RETURN t, r, n 
            LIMIT 50";

        await using var command = connection.CreateCypherCommand(
            _graphName, cypher,
            new Dictionary<string, object?> { { DigitalTwinsJsonPropertyNames.TwinIdParameter, twinId } }
        );
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<object>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var agResultT = (Vertex)(await reader.GetFieldValueAsync<Agtype?>(0))!;
            var agResultR = await reader.GetFieldValueAsync<Agtype>(1);
            var agResultN = (Vertex)(await reader.GetFieldValueAsync<Agtype?>(2))!;

            object relationshipData;
            if (agResultR.IsArray)
            {
                var edges = agResultR.GetList();
                relationshipData = edges is { Count: 1 } && edges[0] is Edge singleEdge
                    ? singleEdge.Label
                    : edges?.Select(e => (object?)(e is Edge edge ? edge.Label : null)).ToList() ?? [];
            }
            else
            {
                relationshipData = ((Edge)agResultR).Label;
            }

            results.Add(
                new
                {
                    sourceId = agResultT.Properties[DigitalTwinsJsonPropertyNames.DigitalTwinId],
                    relationship = relationshipData,
                    targetId = agResultN.Properties[DigitalTwinsJsonPropertyNames.DigitalTwinId],
                    target = agResultN.Properties,
                }
            );
        }

        return JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions { WriteIndented = true }
        );
    }

    /// <summary>
    /// Performs a hybrid search using vector similarity and metadata filtering.
    /// </summary>
    public virtual async Task<string> HybridSearchAsync(
        double[] vector,
        string embeddingProperty,
        string? modelFilter,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );

        string vectorString = JsonSerializer.Serialize(vector);
        string whereClause = !string.IsNullOrEmpty(modelFilter)
            ? " WHERE t.`$metadata`.`$model` = $modelFilter "
            : "";

        // Ensure we cast the array to vector using ::vector
        string cypher =
            $@"
            MATCH (t:Twin)
            {whereClause}
            RETURN t
            ORDER BY l2_distance(t.{embeddingProperty}, {vectorString}::vector) ASC
            LIMIT {limit}";

        var parameters = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(modelFilter))
        {
            parameters["modelFilter"] = modelFilter;
        }

        await using var command = connection.CreateCypherCommand(_graphName, cypher, parameters);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult!;
                results.Add(vertex!.Properties);
            }
            return JsonSerializer.Serialize(
                results,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch (Exception ex)
        {
            return $"Error performing hybrid search: {ex.Message}. Ensure pgvector is enabled and property '{embeddingProperty}' contains valid vectors.";
        }
    }
}
