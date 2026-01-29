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
                            if (((Agtype)value).IsVertex)
                            {
                                var props = ((Vertex)value).Properties;
                                row.Add(column.ColumnName, props);
                                totalProperties += props.Count;
                            }
                            else if (((Agtype)value).IsEdge)
                            {
                                var props = ((Edge)value).Properties;
                                row.Add(column.ColumnName, props);
                                totalProperties += props.Count;
                            }
                            else
                            {
                                string valueString = ((Agtype)value).GetString().Trim('\u0001');
                                if (int.TryParse(valueString, out int intValue))
                                {
                                    row.Add(column.ColumnName, intValue);
                                }
                                else if (double.TryParse(valueString, out double doubleValue))
                                {
                                    row.Add(column.ColumnName, doubleValue);
                                }
                                else if (bool.TryParse(valueString, out bool boolValue))
                                {
                                    row.Add(column.ColumnName, boolValue);
                                }
                                else if (valueString.StartsWith('"') && valueString.EndsWith('"'))
                                {
                                    row.Add(column.ColumnName, valueString.Trim('"'));
                                }
                                else if (valueString.StartsWith('[') && valueString.EndsWith(']'))
                                {
                                    row.Add(column.ColumnName, ((Agtype)value).GetList());
                                }
                                else if (valueString.StartsWith('{') && valueString.EndsWith('}'))
                                {
                                    var dict = JsonSerializer.Deserialize<
                                        Dictionary<string, object>
                                    >(valueString);
                                    if (dict != null)
                                    {
                                        row.Add(column.ColumnName, dict);
                                        totalProperties += dict.Count;
                                    }
                                    else
                                    {
                                        row.Add(column.ColumnName, valueString);
                                    }
                                }
                                else
                                {
                                    row.Add(column.ColumnName, valueString);
                                }
                            }
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
}
