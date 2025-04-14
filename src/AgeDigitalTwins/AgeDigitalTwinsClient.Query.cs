using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
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
    public virtual async IAsyncEnumerable<T?> QueryAsync<T>(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        string cypher;
        if (
            query.Contains("SELECT", StringComparison.InvariantCultureIgnoreCase)
            && !query.Contains("RETURN", StringComparison.InvariantCultureIgnoreCase)
        )
        {
            cypher = AdtQueryHelpers.ConvertAdtQueryToCypher(query, _graphName);
        }
        else
        {
            cypher = query;
        }

        // Detect variable-length edge query using regex
        // VLE queries can't run on read-only connections
        var isVariableLengthEdgeQuery = VariableLengthEdgeRegex().IsMatch(cypher);

        await using var connection = await _dataSource.OpenConnectionAsync(
            isVariableLengthEdgeQuery
                ? Npgsql.TargetSessionAttributes.ReadWrite
                : Npgsql.TargetSessionAttributes.PreferStandby,
            cancellationToken
        );

        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken)
            ?? throw new InvalidOperationException("Reader is null");

        var schema = await reader.GetColumnSchemaAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Dictionary<string, object> row = new();
            // iterate over columns
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
                    else if (valueString.StartsWith('\"') && valueString.EndsWith('\"'))
                    {
                        row.Add(column.ColumnName, valueString.Trim('\"'));
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
                    yield return (T)(object)JsonSerializer.Serialize(value);
                }
                else
                {
                    yield return (T)(object)JsonSerializer.Serialize(row);
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
                yield return JsonSerializer.Deserialize<T>(json);
            }
        }
    }

    [GeneratedRegexAttribute(@"\[[^\]]*(?::\w*)?\*[\d.]*\]", RegexOptions.Compiled)]
    internal static partial Regex VariableLengthEdgeRegex();
}
