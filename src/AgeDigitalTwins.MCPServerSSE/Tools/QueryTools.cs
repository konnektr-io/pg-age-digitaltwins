using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;

namespace AgeDigitalTwins.MCPServerSSE.Tools;

[McpServerToolType, Authorize("age-dt")]
public static class QueryTools
{
    [McpServerTool, Description("Executes a Cypher query.")]
    public static async Task<List<JsonObject>> ExecuteCypherQuery(
        AgeDigitalTwinsClient client,
        [Description("The Cypher query to execute")] string query,
        CancellationToken cancellationToken = default
    )
    {
        List<JsonObject> results = [];

        await foreach (var result in client.QueryAsync<JsonObject>(query, cancellationToken))
        {
            if (result is JsonObject obj)
            {
                results.Add(obj);
            }
        }
        return results;
    }
}
