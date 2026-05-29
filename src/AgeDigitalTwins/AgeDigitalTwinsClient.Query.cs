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
                            DiagnosticConstants.ActivityEventException,
                            default,
                            new ActivityTagsCollection
                            {
                                { DiagnosticConstants.ActivityTagExceptionType, ex.GetType().FullName },
                                { DiagnosticConstants.ActivityTagExceptionMessage, ex.Message },
                                { DiagnosticConstants.ActivityTagExceptionStackTrace, ex.StackTrace },
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
    /// Converts an <see cref="Agtype"/> value to a plain .NET object by materializing
    /// it as a <see cref="JsonElement"/> and walking the JSON tree recursively.
    /// Vertices and edges (identified by <c>$type: "vertex"</c> or <c>"edge"</c>)
    /// have their <c>.properties</c> extracted so the output matches ADT's flattened
    /// twin/relationship format.
    /// </summary>
    internal static (object? Value, int PropertiesCount) ConvertAgtypeToObject(Agtype agtype)
    {
        if (agtype.IsNull)
            return (null, 0);

        var element = agtype.Get<JsonElement>();
        return ConvertJsonElement(element);
    }

    private static (object? Value, int PropertiesCount) ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return (null, 0);

            case JsonValueKind.String:
            {
                var s = element.GetString()!;
                if (bool.TryParse(s, out bool boolVal))
                    return (boolVal, 0);
                return (s, 0);
            }

            case JsonValueKind.Number:
            {
                if (element.TryGetInt32(out int intVal))
                    return (intVal, 0);
                return (element.GetDouble(), 0);
            }

            case JsonValueKind.True:
                return (true, 0);

            case JsonValueKind.False:
                return (false, 0);

            case JsonValueKind.Object:
            {
                // Vertices/edges from AGE have a $type discriminator:
                // {"$type":"vertex","id":1,"label":"Twin","properties":{...}}
                if (element.TryGetProperty("$type", out var typeProp))
                {
                    var typeStr = typeProp.GetString();
                    if (typeStr is "vertex" or "edge")
                    {
                        if (element.TryGetProperty("properties", out var props))
                        {
                            var (val, _) = ConvertJsonElement(props);
                            if (val is Dictionary<string, object?> dict)
                                return (dict, dict.Count);
                        }
                        return (new Dictionary<string, object?>(), 0);
                    }
                }
                // Regular map / untyped object
                var result = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    var (val, _) = ConvertJsonElement(prop.Value);
                    result[prop.Name] = val;
                }
                return (result, result.Count);
            }

            case JsonValueKind.Array:
            {
                var list = new List<object?>();
                int totalCount = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var (val, count) = ConvertJsonElement(item);
                    list.Add(val);
                    totalCount += count;
                }
                return (list, totalCount);
            }

            default:
                return (null, 0);
        }
    }
}
