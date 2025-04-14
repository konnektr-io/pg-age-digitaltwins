using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Continuation token for paginated queries.
/// </summary>
public class ContinuationToken
{
    /// <summary>
    /// The row number to skip in the query.
    /// </summary>
    [JsonPropertyName("_tr")]
    public int RowNumber { get; set; }

    /// <summary>
    /// The original cypher query.
    /// </summary>
    [JsonPropertyName("_q")]
    public required string Query { get; set; }

    public static string Serialize(ContinuationToken token)
    {
        var json = JsonSerializer.Serialize(token);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static ContinuationToken? Deserialize(string base64Token)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64Token));
        return JsonSerializer.Deserialize<ContinuationToken>(json);
    }
}
