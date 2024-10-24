using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class JsonToCypherConverter
{
    public static string ConstructProperties(JsonNode jsonNode)
    {
        var properties = new StringBuilder();

        if (jsonNode is JsonObject jsonObject)
        {
            foreach (var property in jsonObject)
            {
                properties.Append($"{property.Key}:{FormatValue(property.Value)},");
            }

            // Remove the trailing comma
            if (properties.Length > 0)
            {
                properties.Length--;
            }
        }

        return properties.ToString();
    }

    public static string FormatValue(JsonNode value)
    {
        return value switch
        {
            JsonObject obj => $"{{{ConstructProperties(obj)}}}",
            JsonArray arr => $"[{string.Join(",", arr.Select(FormatValue))}]",
            JsonValue val => val.ToJsonString(),
            _ => "null"
        };
    }
}