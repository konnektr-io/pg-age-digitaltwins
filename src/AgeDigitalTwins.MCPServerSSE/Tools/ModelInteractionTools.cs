using System.Text.Json;
using Microsoft.SemanticKernel;

namespace AgeDigitalTwins.MCPServerSSE.Tools;

[McpServerToolType]
public static class ModelInteractionTools
{
    [McpServerTool, Description("Generates a Cypher query based on a natural language prompt.")]
    public static async Task<string> GenerateCypherQuery(
        Kernel kernel,
        AgeDigitalTwinsClient client,
        [Description("The natural language prompt for the query")] string prompt,
        CancellationToken cancellationToken = default
    )
    {
        // Get all models to provide context to the LLM
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

        var llmPrompt =
            $"""
            Given the following DTDL models:
            {string.Join("\n", models)}

            Generate a Cypher query for the following prompt: {prompt}

            The query should be a single line of text, with no explanations or markdown.
            Twins are labeled as `Twin` and models are labeled as `Model`.
            Relationships are labeled with the relationship name.
            The `dtmi` of a twin is stored in the `$metadata.$model` property.
            """;

        var result = await kernel.InvokePromptAsync(llmPrompt, cancellationToken: cancellationToken);

        return result.GetValue<string>() ?? "Failed to generate query.";
    }

    [McpServerTool, Description("Executes a Cypher query.")]
    public static async Task<JsonDocument> ExecuteCypherQuery(
        AgeDigitalTwinsClient client,
        [Description("The Cypher query to execute")] string query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await client.QueryAsync(query, cancellationToken);
        return result;
    }
}
