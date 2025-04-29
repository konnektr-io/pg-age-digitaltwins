using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
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
                string cypher;
                // Use query from token if available
                if (continuationToken != null)
                {
                    cypher = continuationToken.Query; // Override query with the one from the token
                }
                // ADT query that needs to be converted
                else if (
                    query.Contains("SELECT", StringComparison.InvariantCultureIgnoreCase)
                    && !query.Contains("RETURN", StringComparison.InvariantCultureIgnoreCase)
                )
                {
                    cypher = AdtQueryHelpers.ConvertAdtQueryToCypher(query, _graphName);
                }
                // New Cypher query
                else
                {
                    cypher = query;
                }

                // Store the query before modifying it for pagination
                // This is used to include in the next continuation token
                // to allow resuming the query from the same point
                string nextContinuationQuery = cypher;

                // Check if the query already contains a LIMIT clause
                var limitMatch = LimitRegex().Match(cypher);
                // Check if the query already contains a SKIP clause
                var skipMatch = SkipRegex().Match(cypher);

                // Handle existing SKIP and LIMIT clauses
                if (skipMatch.Success)
                {
                    int existingSkip = int.Parse(skipMatch.Groups[1].Value);
                    int newSkip = existingSkip + (continuationToken?.RowNumber ?? 0);
                    cypher = SkipRegex().Replace(cypher, $"SKIP {newSkip}");
                }
                // Handle case where there is no existing SKIP but an existing LIMIT
                else if (limitMatch.Success && continuationToken != null)
                {
                    // Remove the existing LIMIT clause
                    cypher = LimitRegex().Replace(cypher, "");

                    // Add SKIP before the LIMIT
                    cypher +=
                        $" SKIP {continuationToken.RowNumber} LIMIT {limitMatch.Groups[1].Value}";
                }
                else if (continuationToken != null)
                {
                    // Add SKIP clause if it doesn't exist
                    cypher += $" SKIP {continuationToken.RowNumber}";
                }

                int existingLimit = int.MaxValue;
                if (limitMatch.Success)
                {
                    existingLimit = int.Parse(limitMatch.Groups[1].Value);
                    if (maxItemsPerPage.HasValue && maxItemsPerPage.Value < existingLimit)
                    {
                        // Replace the existing LIMIT with the smaller maxItemsPerPage
                        cypher = LimitRegex().Replace(cypher, $"LIMIT {maxItemsPerPage.Value}");
                    }
                }
                else if (maxItemsPerPage.HasValue)
                {
                    // Add LIMIT clause if it doesn't exist
                    cypher += $" LIMIT {maxItemsPerPage.Value}";
                }

                // Detect variable-length edge query using regex (need read-write access)
                // This is a workaround for the fact that Age does not support variable-length edge queries
                // On read-only connections
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
                while (await reader.ReadAsync(ct))
                {
                    Dictionary<string, object> row = new();
                    for (int i = 0; i < schema.Count; i++)
                    {
                        var column = schema[i];
                        var value = await reader.GetFieldValueAsync<Agtype?>(i);
                        if (value == null)
                        {
                            continue;
                        }
                        if (((Agtype)value).IsVertex)
                        {
                            row.Add(column.ColumnName, ((Vertex)value).Properties);
                        }
                        else if (((Agtype)value).IsEdge)
                        {
                            row.Add(column.ColumnName, ((Edge)value).Properties);
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
                                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    valueString
                                );
                                if (dict != null)
                                {
                                    row.Add(column.ColumnName, dict);
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
                        // No more results to fetch, so no continuation token
                        ? null
                        // Generate a continuation token (e.g., next row number)
                        : new ContinuationToken
                        {
                            RowNumber = rowNumber,
                            Query = nextContinuationQuery,
                        };

                return (results, nextContinuationToken);
            }
        );
    }

    [GeneratedRegex(@"\[[^\]]*(?::\w*)?\*[\d.]*\]", RegexOptions.Compiled)]
    internal static partial Regex VariableLengthEdgeRegex();

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
}
