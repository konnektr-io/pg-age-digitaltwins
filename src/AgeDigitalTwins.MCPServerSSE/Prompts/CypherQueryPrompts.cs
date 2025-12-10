using Microsoft.AspNetCore.Authorization;

namespace AgeDigitalTwins.MCPServerSSE.Prompts;

[McpServerPromptType, Authorize("age-dt")]
public static class CypherQueryPrompts
{
    [McpServerPrompt, Description("Returns a prompt that can be used to generate a Cypher query from a natural language prompt.")]
    public static async Task<string> GenerateCypherQuery(
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

        return $"""
            Given the following DTDL models:
            {string.Join("\n", models)}

            Generate a Cypher query for the following prompt: {prompt}

            The query should be a single line of text, with no explanations or markdown.
            Twins are labeled as `Twin` and models are labeled as `Model`.
            Relationships are labeled with the relationship name.
            The `dtmi` of a twin is stored in the `$metadata.$model` property.
            """;
    }

    [McpServerPrompt, Description("Returns a prompt that explains how to run Cypher queries.")]
    public static string ExplainCypherQuery()
    {
        return """
            To run a Cypher query, use the `ExecuteCypherQuery` tool.
            The `ExecuteCypherQuery` tool takes a single parameter, `query`, which is the Cypher query to execute.
            The query should be a single line of text, with no explanations or markdown.
            Twins are labeled as `Twin` and models are labeled as `Model`.
            Relationships are labeled with the relationship name.
            The `dtmi` of a twin is stored in the `$metadata.$model` property.
            """;
    }
}
