namespace AgeDigitalTwins.MCPServerSSE.Prompts;

[McpServerPromptType]
public static class CypherQueryPrompt
{
    public static string GenerateQueryPrompt()
    {
        return @"To generate a Cypher query for AgeDigitalTwins, follow these rules:

1. **Schema Awareness**: Always check the DTDL model (use `GetModel`) to understand the properties and relationships of the twins you are querying. `GetModel` returns a flattened view including inherited properties.

2. **Nodes & Labels**: 
   - Digital Twins always have the `:Twin` label.
   - DTDL Models always have the `:Model` label.
   - Relationships are edges with the relationship name as the label.

3. **Inheritance (Optimized)**: 
   To query all twins of a specific model AND its subtypes, do NOT use `IS_OF_MODEL`. Instead, use this optimized pattern:
   ```cypher
   MATCH (m1:Model {id: 'target_dtmi'})<-[:_extends*0..]-(m2:Model)
   WITH collect(m2.id) AS model_ids
   MATCH (t:Twin)
   WHERE t.`$metadata`.`$model` IN model_ids
   RETURN t
   ```

4. **Vector / Hybrid Search**:
   - If a property is defined as an embedding (Array<Double>), use pgvector functions.
   - Syntax: `MATCH (t:Twin) RETURN t ORDER BY l2_distance(t.propertyName, [vector_values]) ASC LIMIT 10`
   - Always verify the embedding property name from the DTDL model.

5. **Filtering**:
   - Access properties directly, e.g., `t.temperature`.
   - Access metadata via `t.`$metadata`.`$model`` or `t.`$dtId``.
";
    }
}
