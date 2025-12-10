using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace AgeDigitalTwins.MCPServerSSE.Tools;

[McpServerToolType, Authorize("age-dt")]
public static class ModelInteractionTools
{
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
