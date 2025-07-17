using System.ComponentModel;
using System.Text.Json;
using AgeDigitalTwins;
using Json.Patch;
using ModelContextProtocol.Server;

namespace AgeDigitalTwins.MCPServerSSE.Tools;

[McpServerToolType]
public static class DigitalTwinsTools
{
    [McpServerTool, Description("Creates or replaces a digital twin.")]
    public static async Task<string> CreateOrReplaceDigitalTwin(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The digital twin JSON")] JsonDocument digitalTwin,
        [Description("Optional ETag for conditional creation")] string? etag = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await client.CreateOrReplaceDigitalTwinAsync(
            twinId,
            digitalTwin,
            etag,
            cancellationToken
        );
        return result?.ToString() ?? "Digital twin creation or replacement failed.";
    }

    [McpServerTool, Description("Updates a digital twin.")]
    public static async Task UpdateDigitalTwin(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The JSON Patch document")] JsonPatch patch,
        [Description("Optional ETag for conditional update")] string? etag = null,
        CancellationToken cancellationToken = default
    )
    {
        await client.UpdateDigitalTwinAsync(twinId, patch, etag, cancellationToken);
    }

    [McpServerTool, Description("Deletes a digital twin.")]
    public static async Task<string> DeleteDigitalTwin(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await client.DeleteDigitalTwinAsync(twinId, cancellationToken);
            return $"Digital twin with ID '{twinId}' deleted successfully.";
        }
        catch
        {
            return $"Failed to delete digital twin with ID '{twinId}'. It may not exist.";
        }
    }

    [McpServerTool, Description("Fetches a digital twin by ID.")]
    public static async Task<string> GetDigitalTwin(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        CancellationToken cancellationToken = default
    )
    {
        var twin = await client.GetDigitalTwinAsync<JsonElement>(twinId, cancellationToken);
        return twin.ToString() ?? $"Digital twin with ID '{twinId}' not found.";
    }

    [McpServerTool, Description("Fetches all DTDL models.")]
    public static async Task<IEnumerable<string>> GetModels(
        AgeDigitalTwinsClient client,
        CancellationToken cancellationToken = default
    )
    {
        var models = new List<string>();
        await foreach (
            var model in client.GetModelsAsync(
                new() { IncludeModelDefinition = true },
                cancellationToken
            )
        )
        {
            if (model is not null)
            {
                models.Add(model.DtdlModel!);
            }
        }
        return models.Count != 0 ? models : new[] { "No models found." };
    }

    [McpServerTool, Description("Fetches relationships for a digital twin.")]
    public static async Task<IEnumerable<string>> GetRelationships(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("Optional relationship name filter")] string? relationshipName = null,
        CancellationToken cancellationToken = default
    )
    {
        var relationships = new List<string>();
        await foreach (
            var relationship in client.GetRelationshipsAsync<JsonElement>(
                twinId,
                relationshipName,
                cancellationToken
            )
        )
        {
            relationships.Add(relationship.ToString());
        }
        return relationships.Count != 0
            ? relationships
            : new[] { $"No relationships found for twin ID '{twinId}'." };
    }

    [McpServerTool, Description("Fetches a specific relationship by ID.")]
    public static async Task<string> GetRelationship(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The ID of the relationship")] string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        var relationship = await client.GetRelationshipAsync<JsonElement>(
            twinId,
            relationshipId,
            cancellationToken
        );
        return relationship.ToString()
            ?? $"Relationship with ID '{relationshipId}' for twin ID '{twinId}' not found.";
    }

    [McpServerTool, Description("Creates or replaces a relationship.")]
    public static async Task<string> CreateOrReplaceRelationship(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The ID of the relationship")] string relationshipId,
        [Description("The relationship JSON")] JsonDocument relationship,
        [Description("Optional ETag for conditional creation")] string? etag = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await client.CreateOrReplaceRelationshipAsync(
            twinId,
            relationshipId,
            relationship,
            etag,
            cancellationToken
        );
        return result?.ToString() ?? "Relationship creation or replacement failed.";
    }

    [McpServerTool, Description("Updates a relationship.")]
    public static async Task UpdateRelationship(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The ID of the relationship")] string relationshipId,
        [Description("The JSON Patch document")] JsonPatch patch,
        [Description("Optional ETag for conditional update")] string? etag = null,
        CancellationToken cancellationToken = default
    )
    {
        await client.UpdateRelationshipAsync(
            twinId,
            relationshipId,
            patch,
            etag,
            cancellationToken
        );
    }

    [McpServerTool, Description("Deletes a relationship.")]
    public static async Task<string> DeleteRelationship(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The ID of the relationship")] string relationshipId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await client.DeleteRelationshipAsync(twinId, relationshipId, cancellationToken);
            return $"Relationship with ID '{relationshipId}' for twin ID '{twinId}' deleted successfully.";
        }
        catch (ArgumentNullException ex)
        {
            return $"Failed to delete relationship: {ex.Message}. One or more required arguments were null.";
        }
        catch (ArgumentException ex)
        {
            return $"Failed to delete relationship: {ex.Message}. Invalid argument provided.";
        }
        catch (InvalidOperationException ex)
        {
            return $"Failed to delete relationship: {ex.Message}. The operation is not valid in the current state.";
        }
        catch (Exception ex)
        {
            return $"Failed to delete relationship: {ex.Message}. An unexpected error occurred.";
        }
    }
}
