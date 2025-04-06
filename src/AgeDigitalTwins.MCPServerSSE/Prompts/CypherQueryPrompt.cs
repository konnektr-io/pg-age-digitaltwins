using ModelContextProtocol.Server;

namespace AgeDigitalTwins.MCPServerSSE.Prompts;

[McpServerPromptType]
public static class CypherQueryPrompt
{
    [McpServerPrompt, Description("Guide for generating Digital Twin Cypher queries.")]
    public static string GenerateQueryPrompt()
    {
        return "To generate a Cypher query, consider the DTDL metadata and relationships. Use MATCH clauses for relationships and WHERE clauses for filtering. Digital Twins always have the 'Twin' label and DTDL models always have Model label. Relationships have their relationship name as a label. Refer to the schema in the DTDL models for property names and types.";
    }
}
