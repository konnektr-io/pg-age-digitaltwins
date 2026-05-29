using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Executes a query asynchronously and returns the results as an asynchronous enumerable.
    /// </summary>
    /// <typeparam name="T">The type to which the query results will be deserialized.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable of query results.</returns>
    public virtual AsyncPageable<T?> QueryAsync<T>(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        return new AsyncPageable<T?>(
            async (continuationToken, maxItemsPerPage, ct) =>
            {
                using var activity = ActivitySource.StartActivity(
                    "QueryAsync",
                    ActivityKind.Client
                );
                activity?.SetTag("query", query);
                activity?.SetTag("graphName", _graphName);
                try
                {
                    string cypher;
                    if (continuationToken != null)
                    {
                        cypher = continuationToken.Query;
                    }
                    else if (
                        !string.IsNullOrEmpty(query)
                        && query.Contains("SELECT", StringComparison.InvariantCultureIgnoreCase)
                        && query.IndexOf("RETURN", StringComparison.InvariantCultureIgnoreCase) < 0
                    )
                    {
                        cypher = AdtQueryHelpers.ConvertAdtQueryToCypher(query, _graphName);
                    }
                    else if (!string.IsNullOrEmpty(query))
                    {
                        cypher = query;
                    }
                    else
                    {
                        throw new ArgumentNullException(
                            nameof(query),
                            "Query cannot be null or empty."
                        );
                    }
                    activity?.SetTag("cypher", query);

                    string nextContinuationQuery = cypher;
                    var limitMatch = LimitRegex().Match(cypher);
                    var skipMatch = SkipRegex().Match(cypher);
                    // Enforce read-only queries by blocking forbidden keywords
                    string[] forbiddenKeywords =
                    {
                        "CREATE ",
                        "DELETE ",
                        "SET ",
                        "MERGE ",
                        "REMOVE ",
                    };
                    foreach (var keyword in forbiddenKeywords)
                    {
                        if (
                            !string.IsNullOrEmpty(query)
                            && query.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            throw new InvalidAdtQueryException(
                                $"Query contains forbidden keyword: {keyword}. Only read-only queries are allowed."
                            );
                        }
                    }

                    if (skipMatch.Success)
                    {
                        int existingSkip = int.Parse(skipMatch.Groups[1].Value);
                        int newSkip = existingSkip + (continuationToken?.RowNumber ?? 0);
                        cypher = SkipRegex().Replace(cypher, $"SKIP {newSkip}");
                    }
                    else if (limitMatch.Success && continuationToken != null)
                    {
                        cypher = LimitRegex().Replace(cypher, "");

                        cypher +=
                            $" SKIP {continuationToken.RowNumber} LIMIT {limitMatch.Groups[1].Value}";
                    }
                    else if (continuationToken != null)
                    {
                        cypher += $" SKIP {continuationToken.RowNumber}";
                    }

                    int existingLimit = int.MaxValue;
                    if (limitMatch.Success)
                    {
                        existingLimit = int.Parse(limitMatch.Groups[1].Value);
                        if (maxItemsPerPage.HasValue && maxItemsPerPage.Value < existingLimit)
                        {
                            cypher = LimitRegex().Replace(cypher, $"LIMIT {maxItemsPerPage.Value}");
                        }
                    }
                    else if (maxItemsPerPage.HasValue)
                    {
                        cypher += $" LIMIT {maxItemsPerPage.Value}";
                    }

                    var isVariableLengthEdgeQuery = VariableLengthEdgeRegex().IsMatch(cypher);

                    await using var connection = await _dataSource.OpenConnectionAsync(
                        isVariableLengthEdgeQuery
                            ? Npgsql.TargetSessionAttributes.ReadWrite
                            : Npgsql.TargetSessionAttributes.PreferStandby,
                        ct
                    );
                    await using var command = connection.CreateCypherCommand(_graphName, cypher);

                    await using var reader =
                        await command.ExecuteReaderAsync(ct)
                        ?? throw new InvalidOperationException("Reader is null");

                    var schema = await reader.GetColumnSchemaAsync(ct);
                    List<T?> results = new();
                    int totalProperties = 0;
                    while (await reader.ReadAsync(ct))
                    {
                        Dictionary<string, object> row = new();
                        for (int i = 0; i < schema.Count; i++)
                        {
                            var column = schema[i];
                            var value = await reader.GetFieldValueAsync<Agtype?>(i);
                            if (value == null)
                                continue;
                            var (colValue, propCount) = ConvertAgtypeToObject((Agtype)value);
                            row.Add(column.ColumnName, colValue!);
                            totalProperties += propCount;
                        }
                        if (typeof(T) == typeof(string))
                        {
                            if (row.Count == 1 && row.TryGetValue("_", out object? value))
                            {
                                results.Add((T)(object)JsonSerializer.Serialize(value));
                            }
                            else
                            {
                                results.Add((T)(object)JsonSerializer.Serialize(row));
                            }
                        }
                        else
                        {
                            string json;
                            if (row.Count == 1 && row.TryGetValue("_", out object? value))
                            {
                                json = JsonSerializer.Serialize(value);
                            }
                            else
                            {
                                json = JsonSerializer.Serialize(row);
                            }
                            results.Add(JsonSerializer.Deserialize<T>(json));
                        }
                    }

                    var rowNumber = (continuationToken?.RowNumber ?? 0) + results.Count;
                    ContinuationToken? nextContinuationToken =
                        results.Count < maxItemsPerPage || rowNumber >= existingLimit
                            ? null
                            : new ContinuationToken
                            {
                                RowNumber = rowNumber,
                                Query = nextContinuationQuery,
                            };

                    int charge = results.Count;
                    if (isVariableLengthEdgeQuery)
                        charge += 10;
                    charge += totalProperties;
                    if (
                        !string.IsNullOrEmpty(cypher)
                        && (
                            cypher.Contains("COUNT", StringComparison.OrdinalIgnoreCase)
                            || cypher.Contains("SUM", StringComparison.OrdinalIgnoreCase)
                            || cypher.Contains("AVG", StringComparison.OrdinalIgnoreCase)
                            || cypher.Contains("MIN", StringComparison.OrdinalIgnoreCase)
                            || cypher.Contains("MAX", StringComparison.OrdinalIgnoreCase)
                            || cypher.Contains("is_of_model", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        charge += 5;
                    }

                    var page = new Page<T?>
                    {
                        Value = results,
                        ContinuationToken = nextContinuationToken?.ToString(),
                        QueryCharge = charge,
                    };

                    return page;
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity?.AddEvent(
                        new ActivityEvent(
                            "Exception",
                            default,
                            new ActivityTagsCollection
                            {
                                { "exception.type", ex.GetType().FullName },
                                { "exception.message", ex.Message },
                                { "exception.stacktrace", ex.StackTrace },
                            }
                        )
                    );
                    throw;
                }
            }
        );
    }

    [GeneratedRegex(
        @"SKIP\s+(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex SkipRegex();

    [GeneratedRegex(
        @"LIMIT\s+(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex LimitRegex();

    [GeneratedRegex(
        @"\[[^\]]*(?::\w*)?\*[\d.]*\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    internal static partial Regex VariableLengthEdgeRegex();

    /// <summary>
    /// Converts an <see cref="Agtype"/> value to a plain .NET object, recursively handling
    /// arrays so that <see cref="Agtype.IsVertex"/>, <see cref="Agtype.IsEdge"/>, etc.
    /// are applied to every element.
    /// </summary>
    /// <returns>
    /// A tuple of the converted value and the total number of properties encountered
    /// (used for query-charge accounting).
    /// </returns>
    private static (object? Value, int PropertiesCount) ConvertAgtypeListElementToObject(
        object? element
    )
    {
        if (element is Agtype agElement)
            return ConvertAgtypeToObject(agElement);
        if (element is Vertex vertex)
            return (vertex.Properties, vertex.Properties.Count);
        if (element is Edge edge)
            return (edge.Properties, edge.Properties.Count);
        if (element is Dictionary<string, object?> dict)
            return (dict, dict.Count);
        if (element is List<object?> nestedList)
        {
            var list = new List<object?>();
            int count = 0;
            foreach (var nested in nestedList)
            {
                var (val, c) = ConvertAgtypeListElementToObject(nested);
                list.Add(val);
                count += c;
            }
            return (list, count);
        }
        return (element, 0);
    }

    private static (object? Value, int PropertiesCount) ConvertAgtypeToObject(Agtype agtype)
    {
        if (agtype.IsVertex)
        {
            var props = agtype.GetVertex().Properties;
            return (props, props.Count);
        }
        if (agtype.IsEdge)
        {
            var props = agtype.GetEdge().Properties;
            return (props, props.Count);
        }
        if (agtype.IsArray)
        {
            var list = new List<object?>();
            int count = 0;
            foreach (var element in agtype.GetList())
            {
                var (val, c) = ConvertAgtypeListElementToObject(element);
                list.Add(val);
                count += c;
            }
            return (list, count);
        }
        if (agtype.IsMap)
        {
            var dict = agtype.GetMap();
            return (dict, dict.Count);
        }
        if (agtype.IsNull)
            return (null, 0);

        var jsonElement = agtype.Get<JsonElement>();
        if (jsonElement.ValueKind == JsonValueKind.String)
        {
            var s = jsonElement.GetString()!;
            if (bool.TryParse(s, out bool boolVal))
                return (boolVal, 0);
            return (s, 0);
        }
        if (jsonElement.ValueKind == JsonValueKind.Number)
        {
            if (jsonElement.TryGetInt32(out int intVal))
                return (intVal, 0);
            return (jsonElement.GetDouble(), 0);
        }
        if (jsonElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return (jsonElement.ValueKind == JsonValueKind.True, 0);
        }
        return (null, 0);
    }
}
