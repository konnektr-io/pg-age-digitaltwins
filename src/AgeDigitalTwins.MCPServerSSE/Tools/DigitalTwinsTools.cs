using System.Text.Json;
using Json.Patch;

namespace AgeDigitalTwins.MCPServerSSE.Tools;

[McpServerToolType]
public static class DigitalTwinsTools
{
    [McpServerTool, Description("Creates or replaces a digital twin.")]
    public static async Task<string> CreateOrReplaceDigitalTwin(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The digital twin JSON")] JsonDocument digitalTwin,
        CancellationToken cancellationToken = default
    )
    {
        var result = await client.CreateOrReplaceDigitalTwinAsync(
            twinId,
            digitalTwin,
            cancellationToken: cancellationToken
        );
        return result?.ToString() ?? "Digital twin creation or replacement failed.";
    }

    [McpServerTool, Description("Updates a digital twin.")]
    public static async Task UpdateDigitalTwin(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The JSON Patch document")] JsonPatch patch,
        CancellationToken cancellationToken = default
    )
    {
        await client.UpdateDigitalTwinAsync(twinId, patch, cancellationToken: cancellationToken);
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
            await client.DeleteDigitalTwinAsync(twinId, cancellationToken: cancellationToken);
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
        var twin = await client.GetDigitalTwinAsync<JsonElement>(twinId, cancellationToken: cancellationToken);
        return twin.ToString();
    }

    [McpServerTool, Description("Fetches the DTDL Model definition. returns the full flattened model definition including inherited properties.")]
    public static async Task<string> GetModel(
        AgeDigitalTwinsClient client,
        [Description("The ID of the model (DTMI)")] string modelId,
        CancellationToken cancellationToken = default
    )
    {
        try 
        {
            var model = await client.GetModelExpandedAsync(modelId, cancellationToken);
            return JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Failed to get model '{modelId}': {ex.Message}";
        }
    }

    [McpServerTool, Description("Fetches a list of all DTDL models (Summary only). Use GetModel for details.")]
    public static async Task<string> GetModels(
        AgeDigitalTwinsClient client,
        CancellationToken cancellationToken = default
    )
    {
        // We only fetch summary properties to save tokens
        var models = new List<object>();
        await foreach (
            var model in client.GetModelsAsync(
                new() { IncludeModelDefinition = false },
                cancellationToken: cancellationToken
            )
        )
        {
            if (model is not null)
            {
                models.Add(new { id = model.Id, displayName = model.LanguageDisplayNames.GetValueOrDefault("en") ?? model.LanguageDisplayNames.Values.FirstOrDefault() });
            }
        }
        return models.Count != 0 
            ? JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true }) 
            : "No models found.";
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
                cancellationToken: cancellationToken
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
            cancellationToken: cancellationToken
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
        CancellationToken cancellationToken = default
    )
    {
        var result = await client.CreateOrReplaceRelationshipAsync(
            twinId,
            relationshipId,
            relationship,
            cancellationToken: cancellationToken
        );
        return result?.ToString() ?? "Relationship creation or replacement failed.";
    }

    [McpServerTool, Description("Updates a relationship.")]
    public static async Task UpdateRelationship(
        AgeDigitalTwinsClient client,
        [Description("The ID of the digital twin")] string twinId,
        [Description("The ID of the relationship")] string relationshipId,
        [Description("The JSON Patch document")] JsonPatch patch,
        CancellationToken cancellationToken = default
    )
    {
        await client.UpdateRelationshipAsync(
            twinId,
            relationshipId,
            patch,
            cancellationToken: cancellationToken
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
            await client.DeleteRelationshipAsync(twinId, relationshipId, cancellationToken: cancellationToken);
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
    [McpServerTool, Description("Explores the graph neighborhood of a specific twin.")]
    public static async Task<string> ExploreGraphNeighborhood(
        AgeDigitalTwinsClient client,
        [Description("The ID of the central twin")] string twinId,
        [Description("Number of hops/levels to explore (default 1)")] int hops = 1,
        CancellationToken cancellationToken = default
    )
    {
        try 
        {
             return await client.ExploreGraphNeighborhoodAsync(twinId, hops, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Failed to explore neighborhood: {ex.Message}";
        }
    }

    [McpServerTool, Description("Performs a hybrid search using vector similarity and metadata filtering.")]
    public static async Task<string> HybridMemorySearch(
        AgeDigitalTwinsClient client,
        [Description("The vector embedding to search for")] double[] vector,
        [Description("The name of the embedding property in the Twins")] string embeddingProperty,
        [Description("Optional DTDL Model ID to filter by")] string? modelFilter = null,
        [Description("Max results to return")] int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.HybridSearchAsync(vector, embeddingProperty, modelFilter, limit, cancellationToken);
        }
        catch (Exception ex)
        {
             return $"Failed to perform hybrid search: {ex.Message}";
        }
    }
}
