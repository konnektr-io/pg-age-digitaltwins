using System;
using System.Diagnostics;
using System.Linq;
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

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Gets a component on a digital twin asynchronously.
    /// </summary>
    /// <typeparam name="T">The type to which the component will be deserialized.</typeparam>
    /// <param name="digitalTwinId">The ID of the digital twin.</param>
    /// <param name="componentName">The name of the component to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved component.</returns>
    /// <exception cref="DigitalTwinNotFoundException">Thrown when the digital twin is not found.</exception>
    /// <exception cref="ComponentNotFoundException">Thrown when the component is not found.</exception>
    public virtual async Task<T> GetComponentAsync<T>(
        string digitalTwinId,
        string componentName,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("GetComponentAsync", ActivityKind.Client);
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("componentName", componentName);

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.PreferStandby,
                cancellationToken
            );

            return await GetComponentAsync<T>(
                connection,
                digitalTwinId,
                componentName,
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

    internal async Task<T> GetComponentAsync<T>(
        NpgsqlConnection connection,
        string digitalTwinId,
        string componentName,
        CancellationToken cancellationToken = default
    )
    {
        // Get the full digital twin
        var digitalTwin =
            await GetDigitalTwinAsync<JsonObject>(connection, digitalTwinId, cancellationToken)
            ?? throw new DigitalTwinNotFoundException(
                $"Digital Twin with ID {digitalTwinId} not found"
            );

        // Check if the component exists
        if (
            !digitalTwin.TryGetPropertyValue(componentName, out JsonNode? componentNode)
            || componentNode == null
        )
        {
            throw new ComponentNotFoundException(
                $"Component '{componentName}' not found on digital twin '{digitalTwinId}'"
            );
        }

        // Validate that this is actually a component according to the model
        await ValidateComponentExistsInModel(
            connection,
            digitalTwin,
            componentName,
            cancellationToken
        );

        // Deserialize and return the component
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(componentNode))
            ?? throw new InvalidOperationException(
                $"Component '{componentName}' could not be deserialized to type {typeof(T).Name}"
            );
    }

    /// <summary>
    /// Updates a component on a digital twin asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin.</param>
    /// <param name="componentName">The name of the component to update.</param>
    /// <param name="patch">The JSON patch document containing the updates.</param>
    /// <param name="ifMatch">The If-Match header value to check for conditional updates.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="DigitalTwinNotFoundException">Thrown when the digital twin is not found.</exception>
    /// <exception cref="ComponentNotFoundException">Thrown when the component is not found.</exception>
    /// <exception cref="ValidationFailedException">Thrown when the component update fails validation.</exception>
    /// <exception cref="PreconditionFailedException">Thrown when the If-Match condition fails.</exception>
    public virtual async Task UpdateComponentAsync(
        string digitalTwinId,
        string componentName,
        JsonPatch patch,
        string? ifMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "UpdateComponentAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("componentName", componentName);

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.PreferPrimary,
                cancellationToken
            );

            await UpdateComponentAsync(
                connection,
                digitalTwinId,
                componentName,
                patch,
                ifMatch,
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

    internal async Task UpdateComponentAsync(
        NpgsqlConnection connection,
        string digitalTwinId,
        string componentName,
        JsonPatch patch,
        string? ifMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        DateTime now = DateTime.UtcNow;

        // Get the current digital twin
        var digitalTwin =
            await GetDigitalTwinAsync<JsonObject>(connection, digitalTwinId, cancellationToken)
            ?? throw new DigitalTwinNotFoundException(
                $"Digital Twin with ID {digitalTwinId} not found"
            );

        // Check if etag matches if If-Match header is provided
        if (!string.IsNullOrEmpty(ifMatch) && !ifMatch.Equals("*"))
        {
            if (
                digitalTwin.TryGetPropertyValue("$etag", out JsonNode? etagNode)
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

        // Validate that the component exists in the model
        var componentSchema = await GetComponentSchemaFromModel(
            connection,
            digitalTwin,
            componentName,
            cancellationToken
        );

        // Check if the component exists on the twin
        if (
            !digitalTwin.TryGetPropertyValue(componentName, out JsonNode? componentNode)
            || componentNode == null
        )
        {
            throw new ComponentNotFoundException(
                $"Component '{componentName}' not found on digital twin '{digitalTwinId}'"
            );
        }

        // Apply the patch to the component
        JsonNode patchedComponentNode = componentNode.DeepClone();
        var patchResult = patch.Apply(patchedComponentNode);

        if (!patchResult.IsSuccess)
        {
            throw new ValidationFailedException(
                $"Failed to apply patch to component '{componentName}': {patchResult.Error}"
            );
        }

        if (patchResult.Result is not JsonObject patchedComponent)
        {
            throw new ValidationFailedException(
                $"Patched component '{componentName}' is not a valid object"
            );
        }

        // Validate the updated component against its schema
        await ValidateComponentAgainstSchema(
            componentSchema,
            patchedComponent,
            componentName,
            cancellationToken
        );

        // Update the component in the digital twin
        digitalTwin[componentName] = patchedComponent;

        // Update metadata
        if (
            digitalTwin.TryGetPropertyValue("$metadata", out JsonNode? metadataNode)
            && metadataNode is JsonObject metadataObject
        )
        {
            // Update global last update time
            metadataObject["$lastUpdateTime"] = now.ToString("o");

            // Update component metadata
            if (
                patchedComponent.TryGetPropertyValue(
                    "$metadata",
                    out JsonNode? componentMetadataNode
                ) && componentMetadataNode is JsonObject componentMetadataObject
            )
            {
                componentMetadataObject["$lastUpdateTime"] = now.ToString("o");
            }
            else
            {
                patchedComponent["$metadata"] = new JsonObject
                {
                    ["$lastUpdateTime"] = now.ToString("o"),
                };
            }

            // Update twin-level metadata for the component
            if (
                metadataObject.TryGetPropertyValue(
                    componentName,
                    out JsonNode? twinComponentMetadataNode
                ) && twinComponentMetadataNode is JsonObject twinComponentMetadataObject
            )
            {
                twinComponentMetadataObject["lastUpdateTime"] = now.ToString("o");
            }
            else
            {
                metadataObject[componentName] = new JsonObject
                {
                    ["lastUpdateTime"] = now.ToString("o"),
                };
            }
        }

        // Generate new ETag
        string newEtag = ETagGenerator.GenerateEtag(digitalTwinId, now);
        digitalTwin["$etag"] = newEtag;

        // Save the updated digital twin
        string updatedDigitalTwinJson = JsonSerializer
            .Serialize(digitalTwin, serializerOptions)
            .Replace("'", "\\'");

        string cypher =
            $@"WITH '{updatedDigitalTwinJson}'::agtype as twin
MERGE (t: Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})
SET t = twin
RETURN t";

        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Failed to update component '{componentName}' on digital twin '{digitalTwinId}'"
            );
        }
    }

    /// <summary>
    /// Validates that a component exists in the digital twin's model.
    /// </summary>
    private async Task ValidateComponentExistsInModel(
        NpgsqlConnection connection,
        JsonObject digitalTwin,
        string componentName,
        CancellationToken cancellationToken = default
    )
    {
        await GetComponentSchemaFromModel(
            connection,
            digitalTwin,
            componentName,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the component schema from the digital twin's model.
    /// </summary>
    private async Task<DTInterfaceInfo> GetComponentSchemaFromModel(
        NpgsqlConnection connection,
        JsonObject digitalTwin,
        string componentName,
        CancellationToken cancellationToken = default
    )
    {
        // Get the model ID from the digital twin
        if (
            !digitalTwin.TryGetPropertyValue("$metadata", out JsonNode? metadataNode)
            || metadataNode is not JsonObject metadataObject
        )
        {
            throw new ValidationFailedException(
                "Digital Twin must have a $metadata property of type object"
            );
        }

        if (
            !metadataObject.TryGetPropertyValue("$model", out JsonNode? modelNode)
            || modelNode is not JsonValue modelValue
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

        // Get and parse the model
        DigitalTwinsModelData modelData =
            await GetModelWithCacheAsync(modelId, cancellationToken)
            ?? throw new ModelNotFoundException($"{modelId} does not exist.");

        var parsedModelEntities = await _modelParser.ParseAsync(
            modelData.DtdlModel,
            cancellationToken: cancellationToken
        );

        DTInterfaceInfo dtInterfaceInfo =
            (DTInterfaceInfo)
                parsedModelEntities.FirstOrDefault(e => e.Value is DTInterfaceInfo).Value
            ?? throw new ModelNotFoundException(
                $"{modelId} or one of its dependencies does not exist."
            );

        // Check if the component exists in the model
        if (
            !dtInterfaceInfo.Contents.TryGetValue(componentName, out DTContentInfo? contentInfo)
            || contentInfo is not DTComponentInfo componentInfo
        )
        {
            throw new ComponentNotFoundException(
                $"Component '{componentName}' is not defined in the model '{modelId}'"
            );
        }

        // Get the component's schema (interface)
        if (componentInfo.Schema is not DTInterfaceInfo componentSchema)
        {
            throw new ValidationFailedException(
                $"Component '{componentName}' does not have a valid interface schema"
            );
        }

        return componentSchema;
    }

    /// <summary>
    /// Validates a component against its DTDL schema.
    /// </summary>
    private Task ValidateComponentAgainstSchema(
        DTInterfaceInfo componentSchema,
        JsonObject component,
        string componentName,
        CancellationToken cancellationToken = default
    )
    {
        var violations = new System.Collections.Generic.List<string>();

        foreach (var kv in component)
        {
            string property = kv.Key;

            // Skip metadata properties
            if (property == "$metadata")
            {
                continue;
            }

            // Check if the property is defined in the component schema
            if (!componentSchema.Contents.TryGetValue(property, out DTContentInfo? contentInfo))
            {
                violations.Add(
                    $"Property '{property}' is not defined in component '{componentName}' schema"
                );
                continue;
            }

            // Validate properties
            if (contentInfo is DTPropertyInfo propertyDef && kv.Value != null)
            {
                JsonElement value = kv.Value.ToJsonDocument().RootElement;
                var validationFailures = propertyDef.Schema.ValidateInstance(value);

                if (validationFailures.Count != 0)
                {
                    violations.AddRange(
                        validationFailures.Select(v =>
                            $"Component '{componentName}' property '{property}': {v}"
                        )
                    );
                }
            }
        }

        if (violations.Count != 0)
        {
            throw new ValidationFailedException(string.Join(" AND ", violations));
        }

        return Task.CompletedTask;
    }
}
