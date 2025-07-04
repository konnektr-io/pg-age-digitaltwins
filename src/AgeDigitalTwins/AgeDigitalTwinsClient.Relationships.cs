using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using Json.Patch;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Checks if a relationship exists asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship to check.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the relationship exists.</returns>
    private async Task<bool> RelationshipExistsAsync(
        string digitalTwinId,
        string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})-[rel {{`$relationshipId`: '{relationshipId.Replace("'", "\\'")}'}}]->(:Twin) RETURN rel";
        await using var connection = await _dataSource.OpenConnectionAsync(
            Npgsql.TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if a relationship's ETag matches asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship to check.</param>
    /// <param name="etag">The ETag to compare.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the ETag matches.</returns>
    private async Task<bool> RelationshipEtagMatchesAsync(
        string digitalTwinId,
        string relationshipId,
        string etag,
        CancellationToken cancellationToken = default
    )
    {
        string cypher =
            $"MATCH (:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})-[rel {{`$relationshipId`: '{relationshipId.Replace("'", "\\'")}'}}]->(:Twin) WHERE rel['$etag'] = '{etag}' RETURN rel";
        await using var connection = await _dataSource.OpenConnectionAsync(
            Npgsql.TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a relationship asynchronously.
    /// </summary>
    /// <typeparam name="T">The type to which the relationship will be deserialized.</typeparam>
    /// <param name="digitalTwinId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved relationship.</returns>
    public virtual async Task<T?> GetRelationshipAsync<T>(
        string digitalTwinId,
        string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "GetRelationshipAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("relationshipId", relationshipId);

        try
        {
            string cypher =
                $"MATCH (:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})-[rel {{`$relationshipId`: '{relationshipId.Replace("'", "\\'")}'}}]->(:Twin) RETURN rel";
            await using var connection = await _dataSource.OpenConnectionAsync(
                Npgsql.TargetSessionAttributes.PreferStandby,
                cancellationToken
            );
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
    /// Retrieves all relationships of a digital twin asynchronously.
    /// </summary>
    /// <typeparam name="T">The type to which the relationships will be deserialized.</typeparam>
    /// <param name="digitalTwinId">The ID of the source digital twin.</param>
    /// <param name="relationshipName">The name of the relationship to filter by (optional).</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable of the retrieved relationships.</returns>
    public virtual AsyncPageable<T?> GetRelationshipsAsync<T>(
        string digitalTwinId,
        string? relationshipName = default,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "GetRelationshipsAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("relationshipName", relationshipName);

        try
        {
            string edgeLabel = !string.IsNullOrEmpty(relationshipName)
                ? $":{relationshipName}"
                : "";
            string cypher =
                $@"MATCH (:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})-[rel{edgeLabel}]->(:Twin) RETURN *";
            return QueryAsync<T>(cypher, cancellationToken);
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
    /// Retrieves all incoming relationships of a digital twin asynchronously.
    /// </summary>
    /// <typeparam name="T">The type to which the relationships will be deserialized.</typeparam>
    /// <param name="digitalTwinId">The ID of the target digital twin.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable of the retrieved incoming relationships.</returns>
    public virtual AsyncPageable<T?> GetIncomingRelationshipsAsync<T>(
        string digitalTwinId,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "GetIncomingRelationshipsAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);

        try
        {
            string cypher =
                $@"MATCH (:Twin)-[rel]->(:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}}) RETURN *";
            return QueryAsync<T>(cypher, cancellationToken);
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
    /// Creates or replaces a relationship asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the relationship to create or replace.</typeparam>
    /// <param name="digitalTwinId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship to create or replace.</param>
    /// <param name="relationship">The relationship object to create or replace.</param>
    /// <param name="ifNoneMatch">The If-None-Match header value to check for conditional creation (optional).</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created or replaced relationship.</returns>
    public virtual async Task<T?> CreateOrReplaceRelationshipAsync<T>(
        string digitalTwinId,
        string relationshipId,
        T relationship,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "CreateOrReplaceRelationshipAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("relationshipId", relationshipId);
        activity?.SetTag("ifNoneMatch", ifNoneMatch);

        try
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
                relationshipObject.TryGetPropertyValue(
                    "$relationshipId",
                    out var relationshipIdNode
                ) && relationshipIdNode is JsonValue relationshipIdValue
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
MATCH (source:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}}),(target:Twin {{`$dtId`: '{targetId.Replace("'", "\\'")}'}})
MERGE (source)-[rel:{relationshipName} {{`$relationshipId`: '{relationshipId.Replace("'", "\\'")}'}}]->(target)
SET rel = relationship
RETURN rel";
            await using var connection = await _dataSource.OpenConnectionAsync(
                Npgsql.TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
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
    /// Updates a relationship asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship to update.</param>
    /// <param name="patch">The JSON patch document containing the updates.</param>
    /// <param name="ifMatch">The If-Match header value to check for conditional updates (optional).</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task UpdateRelationshipAsync(
        string digitalTwinId,
        string relationshipId,
        JsonPatch patch,
        string? ifMatch = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "UpdateRelationshipAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("relationshipId", relationshipId);

        try
        {
            DateTime now = DateTime.UtcNow;

            // Retrieve the current relationship as JsonObject
            var currentRel = await GetRelationshipAsync<JsonObject>(
                digitalTwinId,
                relationshipId,
                cancellationToken
            );
            if (currentRel == null)
            {
                throw new RelationshipNotFoundException(
                    $"Relationship with ID {relationshipId} on {digitalTwinId} not found"
                );
            }

            // Check if etag matches if If-Match header is provided
            if (!string.IsNullOrEmpty(ifMatch) && !ifMatch.Equals("*"))
            {
                if (
                    currentRel.TryGetPropertyValue("$etag", out var etagNode)
                    && etagNode is JsonValue etagValue
                    && etagValue.GetValueKind() == JsonValueKind.String
                    && !etagValue.ToString().Equals(ifMatch, StringComparison.OrdinalIgnoreCase)
                )
                {
                    throw new PreconditionFailedException(
                        $"If-Match: {ifMatch} header value does not match the current ETag value of the relationship twin with id {relationshipId} on twin with id{digitalTwinId}"
                    );
                }
            }

            // Apply the patch locally
            JsonNode patchedRelNode = currentRel.DeepClone();
            try
            {
                var patchResult = patch.Apply(patchedRelNode);
                if (patchResult.Result is null)
                {
                    throw new ValidationFailedException("Failed to apply patch: result is null");
                }
                patchedRelNode = patchResult.Result;
            }
            catch (Exception ex)
            {
                throw new ValidationFailedException($"Failed to apply patch: {ex.Message}");
            }
            if (patchedRelNode is not JsonObject patchedRel)
            {
                throw new ValidationFailedException("Patched relationship is not a valid object");
            }

            // (Future) Validate the patched relationship here
            // TODO: Add validation logic

            // Update $etag
            patchedRel["$etag"] = ETagGenerator.GenerateEtag(
                $"{digitalTwinId}-{relationshipId}",
                now
            );

            // Replace the entire relationship in the database
            string updatedRelJson = JsonSerializer
                .Serialize(patchedRel, serializerOptions)
                .Replace("'", "\\'");
            string cypher =
                $@"MATCH (:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})-[rel {{`$relationshipId`: '{relationshipId.Replace("'", "\\'")}'}}]->(:Twin)
SET rel = '{updatedRelJson}'::agtype";
            await using var connection = await _dataSource.OpenConnectionAsync(
                Npgsql.TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = connection.CreateCypherCommand(_graphName, cypher);
            await command.ExecuteNonQueryAsync(cancellationToken);
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
    /// Deletes a relationship asynchronously.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship to delete.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task DeleteRelationshipAsync(
        string digitalTwinId,
        string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "DeleteRelationshipAsync",
            ActivityKind.Client
        );
        activity?.SetTag("digitalTwinId", digitalTwinId);
        activity?.SetTag("relationshipId", relationshipId);

        try
        {
            string cypher =
                $"MATCH (:Twin {{`$dtId`: '{digitalTwinId.Replace("'", "\\'")}'}})-[rel {{`$relationshipId`: '{relationshipId.Replace("'", "\\'")}'}}]->(:Twin) DELETE rel RETURN COUNT(rel) AS deletedCount";
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
                throw new RelationshipNotFoundException(
                    $"Relationship with ID {relationshipId} on {digitalTwinId} not found"
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
