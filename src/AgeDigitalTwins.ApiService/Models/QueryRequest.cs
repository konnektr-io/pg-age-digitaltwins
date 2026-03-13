using System.Text.Json.Serialization;

namespace AgeDigitalTwins.ApiService.Models;

/// <summary>
/// Request body for the Query endpoint.
/// </summary>
public class QueryRequest
{
    /// <summary>
    /// The query string in Cypher syntax.
    /// Required for the initial query, optional for continuation requests.
    /// </summary>
    [JsonPropertyName("query")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Query { get; set; }

    /// <summary>
    /// Continuation token from a previous query to get the next page of results.
    /// When specified, the query parameter can be omitted as it's embedded in the token.
    /// </summary>
    [JsonPropertyName("continuationToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContinuationToken { get; set; }
}
