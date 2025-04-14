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
    public virtual CustomAsyncPageable<T?> QueryAsync<T>(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        return new CustomAsyncPageable<T?>(
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

                // Add pagination logic to the cypher query
                if (continuationToken != null)
                {
                    cypher += $" SKIP {continuationToken.RowNumber}";
                }

                if (maxItemsPerPage.HasValue)
                {
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

                // Generate a continuation token (e.g., next row number)
                var nextToken = new ContinuationToken
                {
                    RowNumber = (continuationToken?.RowNumber ?? 0) + results.Count,
                    Query = nextContinuationQuery,
                };

                ContinuationToken? nextContinuationToken =
                    results.Count < maxItemsPerPage ? null : nextToken;

                return (results, nextContinuationToken);
            }
        );
    }

    [GeneratedRegex(@"\[[^\]]*(?::\w*)?\*[\d.]*\]", RegexOptions.Compiled)]
    internal static partial Regex VariableLengthEdgeRegex();
}
